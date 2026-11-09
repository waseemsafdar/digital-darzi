using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public sealed record RolePermissionsViewModel(string Role, bool Customisable, List<PermissionGrantViewModel> Permissions);
public sealed record PermissionGrantViewModel(string Permission, bool Granted, bool IsDefault, bool Delegable);
public sealed record UpdateRolePermissionsRequest(Dictionary<string, bool> Permissions);

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid tenantId, IEnumerable<string> roles, string permission, CancellationToken ct = default);

    /// <summary>Every permission the given roles hold in this tenant (union across roles).</summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid tenantId, IEnumerable<string> roles, CancellationToken ct = default);
    Task<ApiResponse<List<RolePermissionsViewModel>>> GetMatrixAsync(CancellationToken ct = default);
    Task<ApiResponse<RolePermissionsViewModel>> UpdateRoleAsync(string role, UpdateRolePermissionsRequest request, CancellationToken ct = default);

    /// <summary>Drops the cached permission rows of a business after its roles changed.</summary>
    void Invalidate(Guid tenantId);
}

/// <summary>
/// Effective permissions = role defaults, then this tenant's overrides (grant or revoke).
/// Overrides only ever apply to <see cref="Permissions.CustomisableRoles"/> and never to
/// <see cref="Permissions.NonDelegable"/> permissions.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public PermissionService(ApplicationDbContext db, IMemoryCache cache, ICurrentUserService currentUser, TimeProvider clock)
    {
        _db = db;
        _cache = cache;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> HasPermissionAsync(Guid tenantId, IEnumerable<string> roles, string permission, CancellationToken ct = default)
    {
        var overrides = tenantId == Guid.Empty ? new List<TenantRolePermission>() : await GetOverridesAsync(tenantId, ct);
        foreach (var role in roles)
        {
            if (Effective(role, permission, overrides)) return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid tenantId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        var overrides = tenantId == Guid.Empty ? new List<TenantRolePermission>() : await GetOverridesAsync(tenantId, ct);
        var roleList = roles.ToList();
        return Permissions.AllPermissions.Where(p => roleList.Any(r => Effective(r, p, overrides))).ToList();
    }

    public async Task<ApiResponse<List<RolePermissionsViewModel>>> GetMatrixAsync(CancellationToken ct = default)
    {
        var overrides = await GetOverridesAsync(_currentUser.TenantId, ct);
        var roles = new[] { AppRoles.Owner, AppRoles.Manager, AppRoles.Receptionist, AppRoles.Karigar };
        var matrix = roles.Select(r => BuildRole(r, overrides)).ToList();
        return ApiResponse<List<RolePermissionsViewModel>>.Ok(matrix);
    }

    public async Task<ApiResponse<RolePermissionsViewModel>> UpdateRoleAsync(string role, UpdateRolePermissionsRequest request, CancellationToken ct = default)
    {
        var canonical = Permissions.CustomisableRoles.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        if (canonical == null)
            return ApiResponse<RolePermissionsViewModel>.Fail($"Role '{role}' cannot be customised. Customisable roles: {string.Join(", ", Permissions.CustomisableRoles)}.");

        var unknown = request.Permissions.Keys.Where(p => !Permissions.IsKnown(p)).ToList();
        if (unknown.Count > 0)
            return ApiResponse<RolePermissionsViewModel>.Fail($"Unknown permission(s): {string.Join(", ", unknown)}.");

        var nonDelegable = request.Permissions.Where(kv => kv.Value && Permissions.NonDelegable.Contains(kv.Key)).Select(kv => kv.Key).ToList();
        if (nonDelegable.Count > 0)
            return ApiResponse<RolePermissionsViewModel>.Fail($"These permissions belong to the Owner only: {string.Join(", ", nonDelegable)}.");

        var tenantId = _currentUser.TenantId;
        var existing = await _db.TenantRolePermissions
            .Where(p => p.TenantId == tenantId && p.RoleName == canonical)
            .ToListAsync(ct);

        var defaults = Permissions.Defaults[canonical];
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var (permission, granted) in request.Permissions)
        {
            var row = existing.FirstOrDefault(e => e.Permission == permission);
            var isDefault = defaults.Contains(permission) == granted;
            if (isDefault)
            {
                if (row != null) _db.TenantRolePermissions.Remove(row);   // back to default → no override row
                continue;
            }
            if (row == null)
            {
                _db.TenantRolePermissions.Add(new TenantRolePermission
                {
                    TenantId = tenantId, RoleName = canonical, Permission = permission,
                    Granted = granted, UpdatedOn = now, UpdatedBy = _currentUser.UserId
                });
            }
            else
            {
                row.Granted = granted;
                row.UpdatedOn = now;
                row.UpdatedBy = _currentUser.UserId;
            }
        }

        await _db.SaveChangesAsync(ct);
        _cache.Remove(Key(tenantId));

        var overrides = await GetOverridesAsync(tenantId, ct);
        return ApiResponse<RolePermissionsViewModel>.Ok(BuildRole(canonical, overrides), "Permissions updated.");
    }

    private static RolePermissionsViewModel BuildRole(string role, List<TenantRolePermission> overrides)
    {
        var defaults = Permissions.Defaults.TryGetValue(role, out var d) ? d : new HashSet<string>();
        var list = Permissions.AllPermissions.Select(p => new PermissionGrantViewModel(
            p, Effective(role, p, overrides), defaults.Contains(p), !Permissions.NonDelegable.Contains(p))).ToList();
        return new RolePermissionsViewModel(role, Permissions.CustomisableRoles.Contains(role), list);
    }

    public void Invalidate(Guid tenantId) => _cache.Remove(Key(tenantId));

    internal static bool Effective(string role, string permission, IReadOnlyCollection<TenantRolePermission> overrides)
    {
        // A business's own role has no defaults: it holds exactly the permissions granted to it,
        // and never an Owner-only one. Overrides are loaded per business, so another business's
        // role name finds no rows here and gets nothing.
        if (AppRoles.IsCustom(role))
            return !Permissions.NonDelegable.Contains(permission)
                   && overrides.Any(x => x.RoleName == role && x.Permission == permission && x.Granted);

        var isDefault = Permissions.Defaults.TryGetValue(role, out var defaults) && defaults.Contains(permission);
        if (!Permissions.CustomisableRoles.Contains(role)) return isDefault;
        if (Permissions.NonDelegable.Contains(permission)) return false;

        var o = overrides.FirstOrDefault(x => x.RoleName == role && x.Permission == permission);
        return o?.Granted ?? isDefault;
    }

    private async Task<List<TenantRolePermission>> GetOverridesAsync(Guid tenantId, CancellationToken ct)
    {
        if (_cache.TryGetValue(Key(tenantId), out List<TenantRolePermission>? cached) && cached != null) return cached;
        var rows = await _db.TenantRolePermissions.AsNoTracking().Where(p => p.TenantId == tenantId).ToListAsync(ct);
        _cache.Set(Key(tenantId), rows, Ttl);
        return rows;
    }

    private static string Key(Guid tenantId) => $"role-permissions:{tenantId}";
}
