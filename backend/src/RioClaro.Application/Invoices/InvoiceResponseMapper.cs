using System.Text.Json;
using RioClaro.Domain.Entities;

namespace RioClaro.Application.Invoices;

public static class InvoiceResponseMapper
{
    public static InvoiceResponse Map(Invoice invoice, bool includeReviewHistory = true) =>
        new(
            invoice.Id,
            invoice.SupplierId,
            invoice.InvoiceNumber,
            invoice.IssueDate,
            invoice.CurrencyCode,
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.Total,
            StatusCode(invoice.Status),
            invoice.UploadedAtUtc,
            invoice.Lines
                .OrderBy(line => line.Id)
                .Select(line => new InvoiceLineResponse(
                    line.Id,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal,
                    line.Confidence))
                .ToList(),
            includeReviewHistory
                ? invoice.ReviewHistory
                    .OrderByDescending(history => history.OccurredAtUtc)
                    .Select(history => new InvoiceReviewHistoryResponse(
                        history.Action,
                        history.DetailsJson,
                        history.OccurredAtUtc,
                        history.PerformedByUserId))
                    .ToList()
                : [],
            invoice.SupplierName,
            invoice.SupplierTaxId,
            invoice.CustomerName,
            invoice.CustomerTaxId,
            invoice.DueDate,
            invoice.PurchaseOrderNumber,
            ExtractionStatusCode(invoice.ExtractionStatus),
            invoice.ExtractionAttemptCount,
            invoice.LastExtractionErrorCode,
            invoice.ExtractionCompletedAtUtc,
            JsonSerializer.Deserialize<Dictionary<string, double?>>(invoice.FieldConfidenceJson),
            JsonSerializer.Deserialize<List<string>>(invoice.ValidationWarningsJson),
            invoice.DuplicateOfInvoiceId);

    public static string StatusCode(InvoiceStatus status) => status switch
    {
        InvoiceStatus.PendingReview => "pending_review",
        InvoiceStatus.Approved => "approved",
        InvoiceStatus.ExtractionFailed => "extraction_failed",
        InvoiceStatus.Extracted => "extracted",
        InvoiceStatus.NeedsValidation => "needs_validation",
        InvoiceStatus.Duplicate => "duplicate",
        _ => "unknown"
    };

    private static string ExtractionStatusCode(InvoiceExtractionStatus status) => status switch
    {
        InvoiceExtractionStatus.Pending => "pending",
        InvoiceExtractionStatus.Processing => "processing",
        InvoiceExtractionStatus.Succeeded => "succeeded",
        InvoiceExtractionStatus.Failed => "failed",
        _ => "unknown"
    };
}
