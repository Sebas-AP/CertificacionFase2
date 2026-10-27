namespace RioClaro.Domain;

public sealed record InvoiceExtractionLine(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    double? Confidence);

public sealed record InvoiceExtractionData(
    string? SupplierName,
    string? SupplierTaxId,
    string? CustomerName,
    string? CustomerTaxId,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? PurchaseOrderNumber,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? Total,
    string? Currency,
    IReadOnlyList<InvoiceExtractionLine> LineItems,
    IReadOnlyDictionary<string, double?> FieldConfidence);
