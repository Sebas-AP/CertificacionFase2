namespace RioClaro.Domain.Entities;

public sealed class Organization
{
    private Organization()
    {
    }

    public Organization(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ICollection<ApplicationUser> Users { get; private set; } = new List<ApplicationUser>();
    public ICollection<Supplier> Suppliers { get; private set; } = new List<Supplier>();
    public ICollection<Invoice> Invoices { get; private set; } = new List<Invoice>();
    public ICollection<Product> Products { get; private set; } = new List<Product>();
}
