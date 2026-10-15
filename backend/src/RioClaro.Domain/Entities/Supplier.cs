namespace RioClaro.Domain.Entities;

public sealed class Supplier
{
    private Supplier()
    {
    }

    public Supplier(Guid id, Guid organizationId, string name, string? taxIdentifier = null)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        TaxIdentifier = taxIdentifier;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? TaxIdentifier { get; private set; }
    public Organization Organization { get; private set; } = null!;
    public ICollection<Invoice> Invoices { get; private set; } = new List<Invoice>();
}
