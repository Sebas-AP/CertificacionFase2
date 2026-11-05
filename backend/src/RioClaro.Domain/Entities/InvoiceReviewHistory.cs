namespace RioClaro.Domain.Entities;

public sealed class InvoiceReviewHistory
{
    private InvoiceReviewHistory()
    {
    }

    public InvoiceReviewHistory(
        Guid id,
        Guid organizationId,
        Guid invoiceId,
        Guid performedByUserId,
        string action,
        string detailsJson,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        InvoiceId = invoiceId;
        PerformedByUserId = performedByUserId;
        Action = action;
        DetailsJson = detailsJson;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Invoice Invoice { get; private set; } = null!;
    public ApplicationUser PerformedByUser { get; private set; } = null!;
}
