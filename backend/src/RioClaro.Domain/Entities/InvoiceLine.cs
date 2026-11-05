namespace RioClaro.Domain.Entities;

public sealed class InvoiceLine
{
    private InvoiceLine()
    {
    }

    public InvoiceLine(
        Guid id,
        Guid organizationId,
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal lineTotal,
        double? confidence = null)
    {
        Id = id;
        OrganizationId = organizationId;
        InvoiceId = invoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
        Confidence = confidence;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public double? Confidence { get; private set; }
    public Invoice Invoice { get; private set; } = null!;
}
