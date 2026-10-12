namespace RioClaro.Application;

public interface ICurrentUserContext
{
    Guid OrganizationId { get; }
    Guid UserId { get; }
    string OrganizationName { get; }
    string UserDisplayName { get; }
    string Role { get; }
    bool IsDemoMode { get; }
}
