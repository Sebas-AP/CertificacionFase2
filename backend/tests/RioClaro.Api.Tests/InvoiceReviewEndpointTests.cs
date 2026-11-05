using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Domain;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Persistence;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class InvoiceReviewEndpointTests
{
    [Fact]
    public async Task InvoiceCanBeReviewedAndApprovedWithAuditHistory()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var invoiceId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            dbContext.Invoices.Add(new Invoice(
                invoiceId,
                DemoIdentity.OrganizationId,
                DemoIdentity.UserId,
                "invoices/demo/review-test.pdf",
                DateTimeOffset.Parse("2026-10-14T12:00:00Z")));
            await dbContext.SaveChangesAsync();
        }

        using var reviewResponse = await client.PutAsJsonAsync(
            $"/api/facturas/{invoiceId}/revision",
            new
            {
                invoiceNumber = "INV-100",
                issueDate = "2026-10-01",
                currencyCode = "MXN",
                subtotal = 100.2500m,
                taxAmount = 16.0400m,
                total = 116.2900m,
                reason = "Corrección verificada"
            });

        Assert.True(
            reviewResponse.StatusCode == HttpStatusCode.OK,
            await reviewResponse.Content.ReadAsStringAsync());
        using var reviewed = JsonDocument.Parse(await reviewResponse.Content.ReadAsStringAsync());
        Assert.Equal("pending_review", reviewed.RootElement.GetProperty("status").GetString());
        Assert.Equal(116.2900m, reviewed.RootElement.GetProperty("total").GetDecimal());
        var editEvent = Assert.Single(reviewed.RootElement.GetProperty("reviewHistory").EnumerateArray());
        Assert.Equal("Reviewed", editEvent.GetProperty("action").GetString());
        Assert.Equal(DemoIdentity.UserId, editEvent.GetProperty("performedByUserId").GetGuid());
        Assert.Contains(
            "invoiceNumber",
            editEvent.GetProperty("detailsJson").GetString(),
            StringComparison.Ordinal);

        using var approveResponse = await client.PostAsync(
            $"/api/facturas/{invoiceId}/confirmacion",
            content: null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        using var approved = JsonDocument.Parse(await approveResponse.Content.ReadAsStringAsync());
        Assert.Equal("approved", approved.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, approved.RootElement.GetProperty("reviewHistory").GetArrayLength());

        using var secondApprove = await client.PostAsync(
            $"/api/facturas/{invoiceId}/confirmacion",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task InvoiceReviewRejectsInvalidValuesAndDoesNotExposeOtherOrganizations()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var otherOrganizationId = Guid.NewGuid();
        var otherOrganizationInvoiceId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            dbContext.Organizations.Add(new Organization(otherOrganizationId, "Otra organización"));
            await dbContext.SaveChangesAsync();

            dbContext.Invoices.Add(new Invoice(
                otherOrganizationInvoiceId,
                otherOrganizationId,
                DemoIdentity.UserId,
                "invoices/other/test.pdf",
                DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.ChangeTracker.Clear();
        }

        using var missingInvoice = await client.GetAsync($"/api/facturas/{otherOrganizationInvoiceId}");
        Assert.True(
            missingInvoice.StatusCode == HttpStatusCode.NotFound,
            await missingInvoice.Content.ReadAsStringAsync());

        using var invalidReview = await client.PutAsJsonAsync(
            $"/api/facturas/{Guid.NewGuid()}/revision",
            new
            {
                invoiceNumber = "INV-1",
                issueDate = "2026-10-01",
                currencyCode = "US",
                subtotal = -1m,
                taxAmount = 0m,
                total = 0m,
                reason = (string?)null
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidReview.StatusCode);
        using var problem = JsonDocument.Parse(await invalidReview.Content.ReadAsStringAsync());
        Assert.Equal("invalid_invoice_review", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvoiceApprovalRequiresMandatoryFields()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var healthResponse = await client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);

        var invoiceId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            dbContext.Invoices.Add(new Invoice(
                invoiceId,
                DemoIdentity.OrganizationId,
                DemoIdentity.UserId,
                "invoices/demo/incomplete.pdf",
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.PostAsync($"/api/facturas/{invoiceId}/confirmacion", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
