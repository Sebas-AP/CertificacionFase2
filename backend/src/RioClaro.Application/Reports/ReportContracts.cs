namespace RioClaro.Application.Reports;

public sealed record CurrencyInvoiceTotal(string CurrencyCode, decimal TotalAmount);

public sealed record ManagementReportResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int ProcessedInvoiceCount,
    int DuplicateInvoiceCount,
    decimal DuplicateRate,
    IReadOnlyList<CurrencyInvoiceTotal> ApprovedTotalsByCurrency,
    int CriticalInventoryItemCount);

public interface IManagementReportService
{
    Task<ManagementReportResponse> GetAsync(
        Guid organizationId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
