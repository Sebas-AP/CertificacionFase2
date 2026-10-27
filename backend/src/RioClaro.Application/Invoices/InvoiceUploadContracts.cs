using RioClaro.Domain;

namespace RioClaro.Application.Invoices;

public sealed record InvoiceBlobReceipt(string ETag);

public sealed record InvoiceFileMetadata(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    int PageCount,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset RetainUntilUtc);

public interface IInvoiceBlobStore
{
    Task<InvoiceBlobReceipt> UploadAsync(
        string blobName,
        Stream content,
        InvoiceFileMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default);
}

public interface IInvoiceDocumentExtractor
{
    Task<InvoiceExtractionData> ExtractAsync(
        Stream pdf,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceUploadResult(
    InvoiceResponse Invoice,
    bool ExtractionSucceeded,
    string? ExtractionErrorCode,
    string? ExtractionErrorMessage = null);

public sealed record InvoiceUploadOperationResult(
    InvoiceUploadResult? Result,
    bool Duplicate,
    Guid? DuplicateInvoiceId,
    string? Conflict,
    bool NotFound = false);

public interface IInvoiceUploadService
{
    Task<InvoiceUploadOperationResult> UploadAsync(
        Stream pdf,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<InvoiceUploadOperationResult> RetryExtractionAsync(
        Guid invoiceId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
