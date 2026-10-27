using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RioClaro.Application.Invoices;
using RioClaro.Domain;
using RioClaro.Domain.Entities;
using RioClaro.Infrastructure.Invoices;
using RioClaro.Infrastructure.Persistence;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class InvoiceRetentionTests
{
    [Fact]
    public async Task DemoRetentionIsIdempotentAndNeverDeletesLegalHoldPdf()
    {
        var blobStore = new TestBlobStore();
        await using var factory = new RioClaroApiFactory(configureTestServices: services =>
        {
            services.RemoveAll<IInvoiceBlobStore>();
            services.AddSingleton<IInvoiceBlobStore>(blobStore);
        });
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);

        var expiredId = Guid.NewGuid();
        var heldId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiredKey = $"organizations/demo/invoices/{expiredId:D}/original.pdf";
        var heldKey = $"organizations/demo/invoices/{heldId:D}/original.pdf";
        blobStore.Seed(expiredKey);
        blobStore.Seed(heldKey);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            var expired = CreateInvoice(expiredId, expiredKey, now.AddDays(-61), now.AddDays(-1));
            var held = CreateInvoice(heldId, heldKey, now.AddDays(-90), now.AddDays(-30));
            held.SetLegalHold(true);
            dbContext.Invoices.AddRange(expired, held);
            await dbContext.SaveChangesAsync();
        }

        using var firstRun = await client.PostAsync("/api/admin/retention/run", content: null);
        Assert.Equal(HttpStatusCode.OK, firstRun.StatusCode);
        using var firstResult = JsonDocument.Parse(await firstRun.Content.ReadAsStringAsync());
        Assert.Equal(1, firstResult.RootElement.GetProperty("deletedPdfCount").GetInt32());
        Assert.Equal(0, firstResult.RootElement.GetProperty("failedPdfCount").GetInt32());
        Assert.Equal(1, blobStore.DeleteCount);

        using var secondRun = await client.PostAsync("/api/admin/retention/run", content: null);
        Assert.Equal(HttpStatusCode.OK, secondRun.StatusCode);
        using var secondResult = JsonDocument.Parse(await secondRun.Content.ReadAsStringAsync());
        Assert.Equal(0, secondResult.RootElement.GetProperty("deletedPdfCount").GetInt32());
        Assert.Equal(1, blobStore.DeleteCount);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
        Assert.NotNull((await verifyDb.Invoices.SingleAsync(item => item.Id == expiredId)).PdfDeletedAtUtc);
        Assert.Null((await verifyDb.Invoices.SingleAsync(item => item.Id == heldId)).PdfDeletedAtUtc);
        Assert.True((await verifyDb.Invoices.SingleAsync(item => item.Id == heldId)).LegalHold);
    }

    [Fact]
    public async Task RetentionRouteAppearsInDemoSwaggerAndReturnsProblemDetailsForUnknownProduct()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);

        using var swaggerResponse = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
        using var swagger = JsonDocument.Parse(await swaggerResponse.Content.ReadAsStringAsync());
        Assert.True(swagger.RootElement.GetProperty("paths")
            .TryGetProperty("/api/admin/retention/run", out _));

        using var missing = await client.GetAsync($"/api/inventario/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

        using var unknownRoute = await client.GetAsync("/api/ruta-inexistente");
        Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
        Assert.Equal("application/problem+json", unknownRoute.Content.Headers.ContentType?.MediaType);
    }

    private static Invoice CreateInvoice(
        Guid id,
        string blobName,
        DateTimeOffset uploadedAtUtc,
        DateTimeOffset retainUntilUtc)
    {
        var invoice = new Invoice(
            id,
            DemoIdentity.OrganizationId,
            DemoIdentity.UserId,
            blobName,
            uploadedAtUtc);
        invoice.SetUploadMetadata(
            "ficticia.pdf",
            "application/pdf",
            100,
            id.ToString("N").PadRight(64, '0'),
            1,
            "\"test-etag\"",
            retainUntilUtc);
        return invoice;
    }

    private sealed class TestBlobStore : IInvoiceBlobStore
    {
        private readonly HashSet<string> _blobs = new(StringComparer.Ordinal);

        public int DeleteCount { get; private set; }

        public void Seed(string blobName) => _blobs.Add(blobName);

        public Task<InvoiceBlobReceipt> UploadAsync(
            string blobName,
            Stream content,
            InvoiceFileMetadata metadata,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(
            string blobName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.Contains(blobName));

        public Task<Stream> OpenReadAsync(
            string blobName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            if (_blobs.Remove(blobName))
            {
                DeleteCount++;
            }

            return Task.CompletedTask;
        }
    }
}
