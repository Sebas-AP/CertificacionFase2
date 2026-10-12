using RioClaro.Application;
using RioClaro.Domain;

namespace RioClaro.Infrastructure;

public sealed class DemoCurrentUserContext : ICurrentUserContext
{
    public Guid OrganizationId => DemoIdentity.OrganizationId;
    public Guid UserId => DemoIdentity.UserId;
    public string OrganizationName => DemoIdentity.OrganizationName;
    public string UserDisplayName => DemoIdentity.UserDisplayName;
    public string Role => DemoIdentity.Role;
    public bool IsDemoMode => true;
}
