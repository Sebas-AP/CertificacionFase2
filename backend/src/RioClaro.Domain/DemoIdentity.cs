namespace RioClaro.Domain;

public static class DemoIdentity
{
    public const string OrganizationIdValue = "11111111-1111-1111-1111-111111111111";
    public const string UserIdValue = "22222222-2222-2222-2222-222222222222";

    public static readonly Guid OrganizationId = Guid.Parse(OrganizationIdValue);
    public static readonly Guid UserId = Guid.Parse(UserIdValue);
    public const string OrganizationName = "Río Claro";
    public const string UserDisplayName = "Instructor Demo";
    public const string Role = "Administrator";
}
