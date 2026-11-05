namespace RioClaro.Application.Invoices;

public sealed record InvoiceRetentionRunResult(
    int DeletedPdfCount,
    int FailedPdfCount,
    DateTimeOffset CompletedAtUtc);

public interface IInvoiceRetentionService
{
    Task<InvoiceRetentionRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default);
}
