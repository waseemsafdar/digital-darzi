using Application.Common;
using Application.Interfaces.Services;
using System.Security.Claims;

namespace API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public Guid TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenantId"), out var id) ? id : Guid.Empty;

    public Guid ShopId =>
        Guid.TryParse(User?.FindFirstValue("shopId"), out var id) ? id : Guid.Empty;

    public string UserName => User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public bool IsSystemAdmin => IsInRole(AppRoles.SystemAdmin);
    public bool IsOwner => IsInRole(AppRoles.Owner);
    public bool IsManager => IsInRole(AppRoles.Manager);
    public bool IsKarigar => IsInRole(AppRoles.Karigar);
    public bool IsReceptionist => IsInRole(AppRoles.Receptionist);
    public bool IsSupervisor => IsOwner || IsManager;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
