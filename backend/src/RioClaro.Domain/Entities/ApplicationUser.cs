namespace RioClaro.Domain.Entities;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(Guid id, Guid organizationId, string displayName, string role)
    {
        Id = id;
        OrganizationId = organizationId;
        DisplayName = displayName;
        Role = role;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public Organization Organization { get; private set; } = null!;
    public ICollection<Invoice> UploadedInvoices { get; private set; } = new List<Invoice>();
}
