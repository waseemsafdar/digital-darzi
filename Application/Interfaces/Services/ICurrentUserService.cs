namespace Application.Interfaces.Services;

public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid ShopId { get; }
    string UserName { get; }
    IEnumerable<string> Roles { get; }
    bool IsInRole(string role);
    bool IsSystemAdmin { get; }
    bool IsOwner { get; }
    bool IsManager { get; }
    bool IsKarigar { get; }
    bool IsReceptionist { get; }

    /// <summary>True for Owner or Manager — the roles that supervise a branch.</summary>
    bool IsSupervisor { get; }

    /// <summary>Client IP of the current request, when there is one.</summary>
    string? IpAddress { get; }
}
