namespace RioClaro.Domain.Entities;

public enum InvoiceStatus
{
    PendingReview = 0,
    Approved = 1,
    ExtractionFailed = 2
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
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string DocumentStorageKey { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public string? CurrencyCode { get; private set; }
    public decimal? Subtotal { get; private set; }
    public decimal? TaxAmount { get; private set; }
    public decimal? Total { get; private set; }
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
        if (Status != InvoiceStatus.PendingReview)
        {
            throw new InvalidOperationException("Solo se pueden corregir facturas pendientes de revisión.");
        }

        if (subtotal < 0 || taxAmount < 0 || total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Los importes no pueden ser negativos.");
        }

        InvoiceNumber = invoiceNumber;
        IssueDate = issueDate;
        CurrencyCode = currencyCode;
        Subtotal = subtotal;
        TaxAmount = taxAmount;
        Total = total;
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
    }
}
