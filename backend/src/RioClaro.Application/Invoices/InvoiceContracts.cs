namespace RioClaro.Application.Invoices;

public sealed record InvoiceLineResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceReviewHistoryResponse(
    string Action,
    string DetailsJson,
    DateTimeOffset OccurredAtUtc,
    Guid PerformedByUserId);

public sealed record InvoiceResponse(
    Guid Id,
    Guid? SupplierId,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? CurrencyCode,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? Total,
    string Status,
    DateTimeOffset UploadedAtUtc,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<InvoiceReviewHistoryResponse> ReviewHistory);

public sealed record InvoicePageResponse(
    IReadOnlyList<InvoiceResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record InvoiceActionResult(
    InvoiceResponse? Invoice,
    bool NotFound,
    string? Conflict);

public sealed record ReviewInvoiceRequest(
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? CurrencyCode,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? Total,
    string? Reason);

public interface IInvoiceReviewService
{
    Task<InvoicePageResponse> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse?> GetByIdAsync(
        Guid organizationId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceActionResult> ReviewAsync(
        Guid organizationId,
        Guid userId,
        Guid invoiceId,
        ReviewInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceActionResult> ApproveAsync(
        Guid organizationId,
        Guid userId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
