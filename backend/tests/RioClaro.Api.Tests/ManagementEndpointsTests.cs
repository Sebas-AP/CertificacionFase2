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

public sealed class ManagementEndpointsTests
{
    [Fact]
    public async Task ChatReturnsProblemDetailsWhenProviderIsNotConfigured()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { message = "Consulta de prueba sin datos reales." });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("chat_not_configured", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReportUsesHalfOpenUploadPeriodAndKeepsCurrencyTotalsSeparate()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);

        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(2);
        var approvedId = Guid.NewGuid();
        var duplicateId = Guid.NewGuid();
        var outsideId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            dbContext.Invoices.AddRange(
                CreateInvoice(approvedId, from, "MXN", 116.25m, approve: true),
                CreateInvoice(duplicateId, from.AddDays(1), "USD", 50m, duplicate: true),
                CreateInvoice(outsideId, to, "MXN", 9m, approve: true));

            var product = new Product(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                "REPORT-001",
                "Producto crítico",
                "unidad");
            dbContext.Products.Add(product);
            dbContext.InventoryItems.Add(new InventoryItem(
                Guid.NewGuid(),
                DemoIdentity.OrganizationId,
                product.Id,
                1m,
                2m));
            await dbContext.SaveChangesAsync();
        }

        var url = $"/api/reportes?fromUtc={Uri.EscapeDataString(from.ToString("O"))}" +
                  $"&toUtc={Uri.EscapeDataString(to.ToString("O"))}";
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = report.RootElement;
        Assert.Equal(from, root.GetProperty("fromUtc").GetDateTimeOffset());
        Assert.Equal(to, root.GetProperty("toUtc").GetDateTimeOffset());
        Assert.Equal(2, root.GetProperty("processedInvoiceCount").GetInt32());
        Assert.Equal(1, root.GetProperty("duplicateInvoiceCount").GetInt32());
        Assert.Equal(0.5m, root.GetProperty("duplicateRate").GetDecimal());
        Assert.Equal(1, root.GetProperty("criticalInventoryItemCount").GetInt32());

        var currencyTotal = Assert.Single(
            root.GetProperty("approvedTotalsByCurrency").EnumerateArray());
        Assert.Equal("MXN", currencyTotal.GetProperty("currencyCode").GetString());
        Assert.Equal(116.25m, currencyTotal.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task EmptyReportReturnsZeroDuplicateRateAndInvalidPeriodUsesProblemDetails()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);

        using var emptyResponse = await client.GetAsync(
            "/api/reportes?fromUtc=2026-10-01T00%3A00%3A00Z&toUtc=2026-10-02T00%3A00%3A00Z");
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        using var empty = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync());
        Assert.Equal(0m, empty.RootElement.GetProperty("duplicateRate").GetDecimal());
        Assert.Empty(empty.RootElement.GetProperty("approvedTotalsByCurrency").EnumerateArray());

        using var invalidResponse = await client.GetAsync(
            "/api/reportes?fromUtc=2026-10-02T00%3A00%3A00Z&toUtc=2026-10-01T00%3A00%3A00Z");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("application/problem+json", invalidResponse.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        Assert.Equal("invalid_report_period", problem.RootElement.GetProperty("code").GetString());
    }

    private static Invoice CreateInvoice(
        Guid invoiceId,
        DateTimeOffset uploadedAtUtc,
        string currency,
        decimal total,
        bool approve = false,
        bool duplicate = false)
    {
        var invoice = new Invoice(
            invoiceId,
            DemoIdentity.OrganizationId,
            DemoIdentity.UserId,
            $"organizations/demo/invoices/{invoiceId:D}/original.pdf",
            uploadedAtUtc);
        invoice.SetUploadMetadata(
            "ficticia.pdf",
            "application/pdf",
            100,
            invoiceId.ToString("N").PadRight(64, '0'),
            1,
            "\"test-etag\"",
            uploadedAtUtc.AddDays(60));
        invoice.ApplyExtraction(
            new InvoiceExtractionData(
                "Proveedor ficticio",
                "RFC-FICTICIO",
                "Organización demo",
                null,
                $"INV-{invoiceId:N}",
                DateOnly.FromDateTime(uploadedAtUtc.UtcDateTime),
                null,
                null,
                total,
                0m,
                total,
                currency,
                [],
                new Dictionary<string, double?>()),
            [],
            null,
            uploadedAtUtc);

        if (duplicate)
        {
            invoice.MarkDuplicate(Guid.NewGuid());
        }
        else if (approve)
        {
            invoice.ApplyReview(
                $"INV-{invoiceId:N}",
                DateOnly.FromDateTime(uploadedAtUtc.UtcDateTime),
                currency,
                total,
                0m,
                total);
            invoice.Approve();
        }

        return invoice;
    }
}
