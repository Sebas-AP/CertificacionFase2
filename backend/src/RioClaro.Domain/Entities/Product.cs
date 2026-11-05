namespace RioClaro.Domain.Entities;

public sealed class Product
{
    private Product()
    {
    }

    public Product(Guid id, Guid organizationId, string sku, string name, string unit)
    {
        Id = id;
        OrganizationId = organizationId;
        Sku = sku;
        Name = name;
        Unit = unit;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public Organization Organization { get; private set; } = null!;
    public InventoryItem? Inventory { get; private set; }
}
