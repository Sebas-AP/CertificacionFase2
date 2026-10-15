namespace RioClaro.Domain.Entities;

public sealed class InventoryItem
{
    private InventoryItem()
    {
    }

    public InventoryItem(
        Guid id,
        Guid organizationId,
        Guid productId,
        decimal quantityOnHand,
        decimal reorderThreshold)
    {
        Id = id;
        OrganizationId = organizationId;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        ReorderThreshold = reorderThreshold;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public decimal ReorderThreshold { get; private set; }
    public Product Product { get; private set; } = null!;
}
