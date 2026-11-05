namespace RioClaro.Domain.Entities;

public enum InvoiceStatus
{
    PendingReview = 0,
    Approved = 1,
    ExtractionFailed = 2,
    Extracted = 3,
    NeedsValidation = 4,
    Duplicate = 5
}

public enum InvoiceExtractionStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed class Invoice
{
    private Invoice()
    {
    }

    public Invoice(
        Guid id,
        Guid organizationId,
        Guid uploadedByUserId,
        string documentStorageKey,
        DateTimeOffset uploadedAtUtc,
        InvoiceStatus status = InvoiceStatus.PendingReview)
    {
        Id = id;
        OrganizationId = organizationId;
        UploadedByUserId = uploadedByUserId;
        DocumentStorageKey = documentStorageKey;
        UploadedAtUtc = uploadedAtUtc;
        Status = status;
        ExtractionStatus = InvoiceExtractionStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string DocumentStorageKey { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public string? InvoiceNumberNormalized { get; private set; }
    public string? SupplierName { get; private set; }
    public string? SupplierTaxId { get; private set; }
    public string? SupplierTaxIdNormalized { get; private set; }
    public string? CustomerName { get; private set; }
    public string? CustomerTaxId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string? PurchaseOrderNumber { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public string? CurrencyCode { get; private set; }
    public decimal? Subtotal { get; private set; }
    public decimal? TaxAmount { get; private set; }
    public decimal? Total { get; private set; }
    public string? OriginalFileName { get; private set; }
    public string? ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public string? Sha256 { get; private set; }
    public int PageCount { get; private set; }
    public string? BlobETag { get; private set; }
    public DateTimeOffset? RetainUntilUtc { get; private set; }
    public bool LegalHold { get; private set; }
    public DateTimeOffset? PdfDeletedAtUtc { get; private set; }
    public InvoiceExtractionStatus ExtractionStatus { get; private set; }
    public int ExtractionAttemptCount { get; private set; }
    public string? LastExtractionErrorCode { get; private set; }
    public DateTimeOffset? ExtractionStartedAtUtc { get; private set; }
    public DateTimeOffset? ExtractionCompletedAtUtc { get; private set; }
    public string FieldConfidenceJson { get; private set; } = "{}";
    public string ValidationWarningsJson { get; private set; } = "[]";
    public Guid? DuplicateOfInvoiceId { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();
    public Organization Organization { get; private set; } = null!;
    public ApplicationUser UploadedByUser { get; private set; } = null!;
    public Supplier? Supplier { get; private set; }
    public ICollection<InvoiceLine> Lines { get; private set; } = new List<InvoiceLine>();
    public ICollection<InvoiceReviewHistory> ReviewHistory { get; private set; } =
        new List<InvoiceReviewHistory>();

    public void ApplyReview(
        string? invoiceNumber,
        DateOnly? issueDate,
        string? currencyCode,
        decimal? subtotal,
        decimal? taxAmount,
        decimal? total)
    {
        if (Status is not (InvoiceStatus.PendingReview or
            InvoiceStatus.Extracted or InvoiceStatus.NeedsValidation))
        {
            throw new InvalidOperationException("Solo se pueden corregir facturas pendientes de revisión.");
        }

        if (subtotal < 0 || taxAmount < 0 || total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Los importes no pueden ser negativos.");
        }

        InvoiceNumber = invoiceNumber;
        InvoiceNumberNormalized = InvoiceFieldNormalizer.NormalizeInvoiceNumber(invoiceNumber);
        IssueDate = issueDate;
        CurrencyCode = currencyCode;
        Subtotal = subtotal;
        TaxAmount = taxAmount;
        Total = total;
        Status = InvoiceStatus.PendingReview;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Approve()
    {
        if (Status != InvoiceStatus.PendingReview)
        {
            throw new InvalidOperationException("Solo se pueden aprobar facturas pendientes de revisión.");
        }

        if (string.IsNullOrWhiteSpace(InvoiceNumber) ||
            IssueDate is null ||
            string.IsNullOrWhiteSpace(CurrencyCode) ||
            Total is null)
        {
            throw new InvalidOperationException(
                "La factura requiere número, fecha, moneda e importe total antes de aprobarse.");
        }

        Status = InvoiceStatus.Approved;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetUploadMetadata(
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        int pageCount,
        string blobETag,
        DateTimeOffset retainUntilUtc)
    {
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        PageCount = pageCount;
        BlobETag = blobETag;
        RetainUntilUtc = retainUntilUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetLegalHold(bool legalHold)
    {
        LegalHold = legalHold;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkPdfDeleted(DateTimeOffset deletedAtUtc)
    {
        if (LegalHold)
        {
            throw new InvalidOperationException("No se puede eliminar un PDF bajo retención legal.");
        }

        PdfDeletedAtUtc ??= deletedAtUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    public bool BeginExtractionAttempt(
        int maxAttempts,
        DateTimeOffset nowUtc,
        TimeSpan inProgressTimeout)
    {
        if (string.IsNullOrWhiteSpace(DocumentStorageKey) ||
            ExtractionAttemptCount >= maxAttempts ||
            (ExtractionStatus != InvoiceExtractionStatus.Pending &&
             ExtractionStatus != InvoiceExtractionStatus.Failed &&
             ExtractionStatus != InvoiceExtractionStatus.Processing) ||
            (ExtractionStatus == InvoiceExtractionStatus.Processing &&
             ExtractionStartedAtUtc > nowUtc.Subtract(inProgressTimeout)))
        {
            return false;
        }

        ExtractionAttemptCount++;
        ExtractionStatus = InvoiceExtractionStatus.Processing;
        ExtractionStartedAtUtc = nowUtc;
        ExtractionCompletedAtUtc = null;
        LastExtractionErrorCode = null;
        ConcurrencyToken = Guid.NewGuid();
        return true;
    }

    public void ApplyExtraction(
        InvoiceExtractionData extraction,
        IReadOnlyList<string> validationWarnings,
        Guid? duplicateOfInvoiceId,
        DateTimeOffset completedAtUtc)
    {
        SupplierName = Limit(extraction.SupplierName, 200);
        SupplierTaxId = Limit(extraction.SupplierTaxId, 50);
        SupplierTaxIdNormalized = InvoiceFieldNormalizer.NormalizeTaxId(extraction.SupplierTaxId);
        CustomerName = Limit(extraction.CustomerName, 200);
        CustomerTaxId = Limit(extraction.CustomerTaxId, 50);
        InvoiceNumber = Limit(extraction.InvoiceNumber, 100);
        InvoiceNumberNormalized = InvoiceFieldNormalizer.NormalizeInvoiceNumber(extraction.InvoiceNumber);
        IssueDate = extraction.InvoiceDate;
        DueDate = extraction.DueDate;
        PurchaseOrderNumber = Limit(extraction.PurchaseOrderNumber, 100);
        CurrencyCode = NormalizeCurrency(extraction.Currency);
        Subtotal = extraction.Subtotal;
        TaxAmount = extraction.TaxAmount;
        Total = extraction.Total;
        FieldConfidenceJson = System.Text.Json.JsonSerializer.Serialize(extraction.FieldConfidence);
        ValidationWarningsJson = System.Text.Json.JsonSerializer.Serialize(validationWarnings);
        ExtractionStatus = InvoiceExtractionStatus.Succeeded;
        LastExtractionErrorCode = null;
        ExtractionCompletedAtUtc = completedAtUtc;
        DuplicateOfInvoiceId = duplicateOfInvoiceId;
        Status = duplicateOfInvoiceId is not null
            ? InvoiceStatus.Duplicate
            : validationWarnings.Count == 0
                ? InvoiceStatus.Extracted
                : InvoiceStatus.NeedsValidation;

        Lines.Clear();
        foreach (var extractedLine in extraction.LineItems)
        {
            if (string.IsNullOrWhiteSpace(extractedLine.Description))
            {
                continue;
            }

            var quantity = extractedLine.Quantity ?? 0m;
            var unitPrice = extractedLine.UnitPrice ?? 0m;
            var lineTotal = extractedLine.LineTotal ?? (quantity * unitPrice);
            if (quantity < 0 || unitPrice < 0 || lineTotal < 0)
            {
                continue;
            }

            Lines.Add(new InvoiceLine(
                Guid.NewGuid(),
                OrganizationId,
                Id,
                Limit(extractedLine.Description, 500)!,
                quantity,
                unitPrice,
                lineTotal,
                extractedLine.Confidence));
        }

        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkExtractionFailed(string errorCode, DateTimeOffset completedAtUtc)
    {
        ExtractionStatus = InvoiceExtractionStatus.Failed;
        LastExtractionErrorCode = Limit(errorCode, 100);
        ExtractionCompletedAtUtc = completedAtUtc;
        Status = InvoiceStatus.ExtractionFailed;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkDuplicate(Guid duplicateOfInvoiceId)
    {
        DuplicateOfInvoiceId = duplicateOfInvoiceId;
        Status = InvoiceStatus.Duplicate;
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string? NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? null : Limit(currency.Trim().ToUpperInvariant(), 3);

    private static string? Limit(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
