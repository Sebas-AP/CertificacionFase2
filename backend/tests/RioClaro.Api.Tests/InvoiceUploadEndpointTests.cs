using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

public sealed class InvoiceUploadEndpointTests
{
    [Fact]
    public async Task UploadRejectsInvalidPdfBeforeCallingExternalServices()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "invoice.pdf", "not a PDF"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_pdf_signature", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, blobStore.UploadCount);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public async Task StorageFailureDoesNotCallExtractorOrPersistInvoice()
    {
        var blobStore = new TestInvoiceBlobStore { FailUpload = true };
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "invoice.pdf", CreateMinimalPdf());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("blob_storage_failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, blobStore.UploadCount);
        Assert.Equal(0, extractor.CallCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
        Assert.Empty(await dbContext.Invoices.ToListAsync());
    }

    [Fact]
    public async Task SqlFailureAfterBlobUploadAttemptsCompensation()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailInvoiceInsert BEFORE INSERT ON Invoices " +
                "BEGIN SELECT RAISE(ABORT, 'forced invoice insert failure'); END;");
        }

        using var response = await UploadAsync(client, "invoice.pdf", CreateMinimalPdf());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(1, blobStore.DeleteCount);
        Assert.Equal(0, blobStore.BlobCount);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public async Task PdfPageLimitAcceptsFiftyAndRejectsFiftyOnePages()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var allowed = await UploadAsync(
            client,
            "fifty-pages.pdf",
            CreateMinimalPdf(pageCount: 50));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var accepted = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        var invoiceId = accepted.RootElement.GetProperty("invoice").GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            Assert.Equal(50, (await dbContext.Invoices.SingleAsync(item => item.Id == invoiceId)).PageCount);
        }

        using var rejected = await UploadAsync(
            client,
            "fifty-one-pages.pdf",
            CreateMinimalPdf(pageCount: 51));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("pdf_page_limit_exceeded", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public async Task UploadRejectsOneByteAboveTwentyMiB()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();
        var oversizedPdf = new byte[20_971_521];
        "%PDF-"u8.CopyTo(oversizedPdf);

        using var response = await UploadAsync(client, "oversized.pdf", oversizedPdf);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("pdf_too_large", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, blobStore.UploadCount);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public async Task UploadPersistsPdfWhenExtractionIsUnavailableAndRetryUsesExistingBlob()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor { Fail = true };
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var uploadResponse = await UploadAsync(client, "invoice.pdf", CreateMinimalPdf());

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        using var upload = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        Assert.False(upload.RootElement.GetProperty("extractionSucceeded").GetBoolean());
        Assert.Equal(
            "Configure Azure Document Intelligence para habilitar la extracción.",
            upload.RootElement.GetProperty("extractionErrorMessage").GetString());
        Assert.Equal(
            "extraction_failed",
            upload.RootElement.GetProperty("invoice").GetProperty("status").GetString());
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(1, extractor.CallCount);
        var invoiceId = upload.RootElement.GetProperty("invoice").GetProperty("id").GetGuid();

        using var firstRetry = await client.PostAsync(
            $"/api/invoices/{invoiceId}/retry-extraction",
            content: null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, firstRetry.StatusCode);

        using var secondRetry = await client.PostAsync(
            $"/api/invoices/{invoiceId}/retry-extraction",
            content: null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, secondRetry.StatusCode);

        using var exhaustedRetry = await client.PostAsync(
            $"/api/invoices/{invoiceId}/retry-extraction",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, exhaustedRetry.StatusCode);
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(3, extractor.CallCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
        var invoice = await dbContext.Invoices.SingleAsync(item => item.Id == invoiceId);
        Assert.Equal(3, invoice.ExtractionAttemptCount);
        Assert.Equal("document_intelligence_not_configured", invoice.LastExtractionErrorCode);
    }

    [Fact]
    public async Task ExactDuplicateIsRejectedBeforeBlobStorageAndExtraction()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();
        var pdf = CreateMinimalPdf();

        using var firstUpload = await UploadAsync(client, "first.pdf", pdf);
        using var duplicateUpload = await UploadAsync(client, "renamed.pdf", pdf);

        Assert.Equal(HttpStatusCode.Created, firstUpload.StatusCode);
        using var firstResult = JsonDocument.Parse(await firstUpload.Content.ReadAsStringAsync());
        Assert.True(firstResult.RootElement.GetProperty("extractionSucceeded").GetBoolean());
        Assert.Equal(
            "extracted",
            firstResult.RootElement.GetProperty("invoice").GetProperty("status").GetString());
        var line = Assert.Single(
            firstResult.RootElement.GetProperty("invoice").GetProperty("lines").EnumerateArray());
        Assert.Equal(0.95, line.GetProperty("confidence").GetDouble());
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpload.StatusCode);
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public async Task BusinessDuplicateMatchesNormalizedSupplierAndInvoiceNumber()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor();
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var firstUpload = await UploadAsync(client, "first.pdf", CreateMinimalPdf());
        using var secondUpload = await UploadAsync(
            client,
            "second.pdf",
            CreateMinimalPdf("different-file-content"));

        Assert.Equal(HttpStatusCode.Created, firstUpload.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondUpload.StatusCode);
        using var first = JsonDocument.Parse(await firstUpload.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await secondUpload.Content.ReadAsStringAsync());
        var firstInvoiceId = first.RootElement.GetProperty("invoice").GetProperty("id").GetGuid();
        var secondInvoice = second.RootElement.GetProperty("invoice");
        Assert.Equal("duplicate", secondInvoice.GetProperty("status").GetString());
        Assert.Equal(
            firstInvoiceId,
            secondInvoice.GetProperty("duplicateOfInvoiceId").GetGuid());
        Assert.Equal(2, blobStore.UploadCount);
        Assert.Equal(2, extractor.CallCount);
    }

    [Fact]
    public async Task ConcurrentRetryDoesNotStartSecondExtraction()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor { Fail = true };
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var uploadResponse = await UploadAsync(client, "invoice.pdf", CreateMinimalPdf());
        using var upload = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        var invoiceId = upload.RootElement.GetProperty("invoice").GetProperty("id").GetGuid();
        extractor.Fail = false;
        extractor.BlockOnCall = 2;
        extractor.RetryGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var inProgressRetry = client.PostAsync(
            $"/api/invoices/{invoiceId}/retry-extraction",
            content: null);
        await extractor.RetryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            var inProgressInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == invoiceId);
            Assert.Equal(2, inProgressInvoice.ExtractionAttemptCount);
            Assert.Equal(InvoiceExtractionStatus.Processing, inProgressInvoice.ExtractionStatus);
        }

        using var competingRetry = await client.PostAsync(
            $"/api/invoices/{invoiceId}/retry-extraction",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, competingRetry.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();
            var inProgressInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == invoiceId);
            Assert.Equal(2, inProgressInvoice.ExtractionAttemptCount);
            Assert.Equal(InvoiceExtractionStatus.Processing, inProgressInvoice.ExtractionStatus);
        }

        extractor.RetryGate.SetResult(true);
        using var completedRetry = await inProgressRetry;
        Assert.True(
            completedRetry.StatusCode == HttpStatusCode.OK,
            await completedRetry.Content.ReadAsStringAsync());
        Assert.Equal(1, blobStore.UploadCount);
        Assert.Equal(2, extractor.CallCount);
    }

    [Fact]
    public async Task InvalidExtractedAmountsBecomeWarningsWithoutDiscardingInvoice()
    {
        var blobStore = new TestInvoiceBlobStore();
        var extractor = new TestInvoiceDocumentExtractor
        {
            Data = new InvoiceExtractionData(
                "Proveedor de demostración",
                "RFC-123",
                "Río Claro",
                null,
                "INV-101",
                new DateOnly(2026, 10, 1),
                null,
                null,
                100m,
                16m,
                decimal.MaxValue,
                "MXN",
                [
                    new InvoiceExtractionLine(
                        "Importe fuera de rango",
                        99_999_999_999_999.9999m,
                        999_999_999_999_999.9999m,
                        null,
                        0.9)
                ],
                new Dictionary<string, double?>
                {
                    ["InvoiceId"] = 0.99,
                    ["InvoiceDate"] = 0.99,
                    ["InvoiceTotal"] = 0.99
                })
        };
        await using var factory = CreateFactory(blobStore, extractor);
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "invoice.pdf", CreateMinimalPdf());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var upload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(upload.RootElement.GetProperty("extractionSucceeded").GetBoolean());
        var invoice = upload.RootElement.GetProperty("invoice");
        Assert.Equal("needs_validation", invoice.GetProperty("status").GetString());
        Assert.True(invoice.GetProperty("total").ValueKind == JsonValueKind.Null);
        var warnings = invoice.GetProperty("validationWarnings")
            .EnumerateArray()
            .Select(warning => warning.GetString())
            .ToArray();
        Assert.Contains("invalid_total", warnings);
        Assert.Contains("invalid_line_item_total", warnings);
        Assert.Empty(invoice.GetProperty("lines").EnumerateArray());
    }

    private static RioClaroApiFactory CreateFactory(
        TestInvoiceBlobStore blobStore,
        TestInvoiceDocumentExtractor extractor) =>
        new(configureTestServices: services =>
        {
            services.RemoveAll<IInvoiceBlobStore>();
            services.RemoveAll<IInvoiceDocumentExtractor>();
            services.AddSingleton<IInvoiceBlobStore>(blobStore);
            services.AddSingleton<IInvoiceDocumentExtractor>(extractor);
        });

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string fileName,
        byte[] fileBytes)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(fileBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        return await client.PostAsync("/api/invoices/upload", content);
    }

    private static byte[] CreateMinimalPdf(string? comment = null, int pageCount = 1)
    {
        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            $"2 0 obj\n<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(3, pageCount).Select(id => $"{id} 0 R"))}] /Count {pageCount} >>\nendobj\n"
        };
        objects.AddRange(Enumerable.Range(3, pageCount).Select(id =>
            $"{id} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>\nendobj\n"));
        var pdf = new StringBuilder("%PDF-1.4\n");
        if (!string.IsNullOrWhiteSpace(comment))
        {
            pdf.Append("% ").Append(comment).Append('\n');
        }

        var offsets = new List<int> { 0 };
        foreach (var pdfObject in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(pdfObject);
        }

        var crossReferenceOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ")
            .Append((objects.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ")
            .Append((objects.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(crossReferenceOffset.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private sealed class TestInvoiceBlobStore : IInvoiceBlobStore
    {
        private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

        public int UploadCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int BlobCount => _blobs.Count;
        public bool FailUpload { get; init; }

        public async Task<InvoiceBlobReceipt> UploadAsync(
            string blobName,
            Stream content,
            InvoiceFileMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            if (FailUpload)
            {
                throw new InvoiceDependencyException("blob_storage", "blob_storage_failed");
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _blobs.Add(blobName, buffer.ToArray());
            UploadCount++;
            return new InvoiceBlobReceipt("\"test-etag\"");
        }

        public Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.ContainsKey(blobName));

        public Task<Stream> OpenReadAsync(
            string blobName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_blobs[blobName], writable: false));

        public Task DeleteIfExistsAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            _blobs.Remove(blobName);
            DeleteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestInvoiceDocumentExtractor : IInvoiceDocumentExtractor
    {
        public bool Fail { get; set; }
        public InvoiceExtractionData? Data { get; init; }
        public int BlockOnCall { get; set; }
        public TaskCompletionSource<bool> RetryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool>? RetryGate { get; set; }
        public int CallCount { get; private set; }

        public async Task<InvoiceExtractionData> ExtractAsync(
            Stream pdf,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == BlockOnCall && RetryGate is not null)
            {
                RetryStarted.TrySetResult(true);
                await RetryGate.Task.WaitAsync(cancellationToken);
            }

            if (Fail)
            {
                throw new InvoiceDependencyException(
                    "document_intelligence",
                    "document_intelligence_not_configured");
            }

            return Data ?? new InvoiceExtractionData(
                "Proveedor de demostración",
                "RFC-123",
                "Río Claro",
                null,
                "INV-100",
                new DateOnly(2026, 10, 1),
                null,
                null,
                100m,
                16m,
                116m,
                "MXN",
                [new InvoiceExtractionLine("Artículo de demostración", 1m, 100m, 100m, 0.95)],
                new Dictionary<string, double?>
                {
                    ["InvoiceId"] = 0.99,
                    ["InvoiceDate"] = 0.99,
                    ["InvoiceTotal"] = 0.99
                });
        }
    }
}
