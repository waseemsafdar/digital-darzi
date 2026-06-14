namespace Application.Interfaces.Services;

public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid ShopId { get; }
    string UserName { get; }
    IEnumerable<string> Roles { get; }
    bool IsInRole(string role);
    bool IsOwner { get; }
    bool IsManager { get; }
    bool IsKarigar { get; }
    bool IsReceptionist { get; }
}
