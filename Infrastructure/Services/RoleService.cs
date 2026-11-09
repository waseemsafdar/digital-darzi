using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.User;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public interface IRoleService
{
    Task<ApiResponse<List<RoleViewModel>>> GetAsync(bool includeHidden, CancellationToken ct = default);
    Task<ApiResponse<RoleViewModel>> CreateAsync(SaveRoleViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<RoleViewModel>> UpdateAsync(Guid id, SaveRoleViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default);
    ApiResponse<List<PermissionInfoViewModel>> GetPermissionCatalogue();

    /// <summary>Null when every role id may be given to staff in this business; otherwise the reason.</summary>
    Task<string?> CheckAssignableAsync(IEnumerable<Guid> roleIds, IEnumerable<Guid> alreadyHeld, CancellationToken ct = default);
}

/// <summary>
/// A business's roles: the built-in ones (renamable, hideable, permissions adjustable) and the
/// business's own roles (fully editable). Owner and SystemAdmin are never editable.
/// </summary>
public sealed class RoleService : IRoleService
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _currentUser;

    public RoleService(ApplicationDbContext db, RoleManager<IdentityRole<Guid>> roleManager, IPermissionService permissions, ICurrentUserService currentUser)
    {
        _db = db;
        _roleManager = roleManager;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<RoleViewModel>>> GetAsync(bool includeHidden, CancellationToken ct = default)
    {
        var all = await LoadAsync(ct);
        return ApiResponse<List<RoleViewModel>>.Ok(all.Where(r => includeHidden || !r.IsHidden).ToList());
    }

    public ApiResponse<List<PermissionInfoViewModel>> GetPermissionCatalogue() =>
        ApiResponse<List<PermissionInfoViewModel>>.Ok(Permissions.AllPermissions
            .Select(p => new PermissionInfoViewModel { Key = p, Delegable = !Permissions.NonDelegable.Contains(p) }).ToList());

    public async Task<ApiResponse<RoleViewModel>> CreateAsync(SaveRoleViewModel vm, CancellationToken ct = default)
    {
        var name = Clean(vm.DisplayName);
        if (CheckPermissions(vm.Permissions) is { } permError) return ApiResponse<RoleViewModel>.Fail(permError);
        var existing = await LoadAsync(ct);
        if (existing.Any(r => string.Equals(r.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            return ApiResponse<RoleViewModel>.Conflict($"A role called '{name}' already exists.");

        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var identityRole = new IdentityRole<Guid>($"{AppRoles.CustomPrefix}{Guid.NewGuid():N}");
        var created = await _roleManager.CreateAsync(identityRole);
        if (!created.Succeeded) return ApiResponse<RoleViewModel>.Fail(created.Errors.Select(e => e.Description).ToList());

        _db.ShopRoles.Add(new ShopRole
        {
            TenantId = tenantId, RoleName = identityRole.Name!, IdentityRoleId = identityRole.Id,
            DisplayName = name, Description = CleanOrNull(vm.Description), IsCustom = true, IsHidden = vm.IsHidden,
            CreatedOn = now, CreatedBy = _currentUser.UserId, UpdatedOn = now, UpdatedBy = _currentUser.UserId
        });
        AddGrants(identityRole.Name!, vm.Permissions, now);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _permissions.Invalidate(tenantId);

        return ApiResponse<RoleViewModel>.Ok((await LoadAsync(ct)).First(r => r.Id == identityRole.Id), "Role added.");
    }

    public async Task<ApiResponse<RoleViewModel>> UpdateAsync(Guid id, SaveRoleViewModel vm, CancellationToken ct = default)
    {
        var all = await LoadAsync(ct);
        var current = all.FirstOrDefault(r => r.Id == id);
        if (current == null) return ApiResponse<RoleViewModel>.NotFound("Role not found.");
        if (current.IsLocked) return ApiResponse<RoleViewModel>.Forbidden("The Owner role cannot be changed.");

        var name = Clean(vm.DisplayName);
        if (CheckPermissions(vm.Permissions) is { } permError) return ApiResponse<RoleViewModel>.Fail(permError);
        if (all.Any(r => r.Id != id && string.Equals(r.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            return ApiResponse<RoleViewModel>.Conflict($"A role called '{name}' already exists.");

        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var row = await _db.ShopRoles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RoleName == current.Name, ct);
        if (row == null)
        {
            row = new ShopRole
            {
                TenantId = tenantId, RoleName = current.Name, IdentityRoleId = id,
                CreatedOn = now, CreatedBy = _currentUser.UserId
            };
            _db.ShopRoles.Add(row);
        }
        row.DisplayName = name;
        row.Description = CleanOrNull(vm.Description);
        row.IsHidden = vm.IsHidden;
        row.UpdatedOn = now;
        row.UpdatedBy = _currentUser.UserId;

        if (current.IsBuiltIn)
        {
            await _db.SaveChangesAsync(ct);
            // Built-in roles keep their defaults; only the differences are stored, as before.
            var wanted = Permissions.AllPermissions.Where(p => !Permissions.NonDelegable.Contains(p))
                .ToDictionary(p => p, p => vm.Permissions.Contains(p));
            var result = await _permissions.UpdateRoleAsync(current.Name, new UpdateRolePermissionsRequest(wanted), ct);
            if (!result.Success) return ApiResponse<RoleViewModel>.Fail(result.Errors.FirstOrDefault() ?? "Could not update permissions.");
        }
        else
        {
            var old = await _db.TenantRolePermissions.Where(p => p.TenantId == tenantId && p.RoleName == current.Name).ToListAsync(ct);
            _db.TenantRolePermissions.RemoveRange(old);
            AddGrants(current.Name, vm.Permissions, now);
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        _permissions.Invalidate(tenantId);

        return ApiResponse<RoleViewModel>.Ok((await LoadAsync(ct)).First(r => r.Id == id), "Role saved.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var current = (await LoadAsync(ct)).FirstOrDefault(r => r.Id == id);
        if (current == null) return ApiResponse<object>.NotFound("Role not found.");
        if (current.IsBuiltIn) return ApiResponse<object>.Fail("Built-in roles cannot be deleted. Hide it instead.");
        if (current.StaffCount > 0)
            return ApiResponse<object>.Conflict($"{current.StaffCount} staff have this role. Give them another role first.");

        var tenantId = _currentUser.TenantId;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.TenantRolePermissions.RemoveRange(
            await _db.TenantRolePermissions.Where(p => p.TenantId == tenantId && p.RoleName == current.Name).ToListAsync(ct));
        _db.ShopRoles.RemoveRange(await _db.ShopRoles.Where(r => r.TenantId == tenantId && r.RoleName == current.Name).ToListAsync(ct));
        await _db.SaveChangesAsync(ct);

        var identityRole = await _roleManager.FindByIdAsync(id.ToString());
        if (identityRole != null) await _roleManager.DeleteAsync(identityRole);
        await tx.CommitAsync(ct);
        _permissions.Invalidate(tenantId);
        return ApiResponse<object>.Ok(null, "Role deleted.");
    }

    public async Task<string?> CheckAssignableAsync(IEnumerable<Guid> roleIds, IEnumerable<Guid> alreadyHeld, CancellationToken ct = default)
    {
        var all = await LoadAsync(ct);
        var held = alreadyHeld.ToHashSet();
        foreach (var id in roleIds.Distinct())
        {
            var role = all.FirstOrDefault(r => r.Id == id);
            if (role == null) return "One or more roles were not found.";
            if (role.IsHidden && !held.Contains(id)) return $"'{role.DisplayName}' is hidden. Show it again under Roles to give it to staff.";
        }
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Every role this business can see, in display order, with its effective permissions.</summary>
    private async Task<List<RoleViewModel>> LoadAsync(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var settings = await _db.ShopRoles.AsNoTracking().Where(r => r.TenantId == tenantId).ToListAsync(ct);
        var customNames = settings.Where(s => s.IsCustom).Select(s => s.RoleName).ToList();
        var builtInNames = AppRoles.BuiltIn.Select(b => b.Name).ToList();
        var identityRoles = await _db.Roles.AsNoTracking()
            .Where(r => builtInNames.Contains(r.Name!) || customNames.Contains(r.Name!))
            .ToDictionaryAsync(r => r.Name!, r => r.Id, ct);

        // Staff per role (tenant-filtered, active accounts only).
        var staffRoles = await _db.AppUsers.AsNoTracking()
            .Where(u => u.ActiveStatus == Domain.Enums.ActiveStatus.Active)
            .Select(u => u.RoleIds).ToListAsync(ct);
        int CountFor(Guid id) => staffRoles.Count(ids => ids.Contains(id));

        var list = new List<RoleViewModel>();
        foreach (var (name, display, description) in AppRoles.BuiltIn)
        {
            if (!identityRoles.TryGetValue(name, out var id)) continue;   // not seeded yet on this server
            var s = settings.FirstOrDefault(x => x.RoleName == name);
            list.Add(await BuildAsync(id, name, s?.DisplayName is { Length: > 0 } d ? d : display, s?.Description ?? description,
                isBuiltIn: true, isHidden: s?.IsHidden ?? false, defaultName: display, CountFor(id), tenantId, ct));
        }
        foreach (var s in settings.Where(x => x.IsCustom).OrderBy(x => x.DisplayName))
        {
            if (!identityRoles.TryGetValue(s.RoleName, out var id)) continue;
            list.Add(await BuildAsync(id, s.RoleName, s.DisplayName, s.Description, isBuiltIn: false, s.IsHidden, null, CountFor(id), tenantId, ct));
        }
        return list;
    }

    private async Task<RoleViewModel> BuildAsync(Guid id, string name, string display, string? description, bool isBuiltIn, bool isHidden,
        string? defaultName, int staffCount, Guid tenantId, CancellationToken ct)
    {
        var granted = await _permissions.GetEffectivePermissionsAsync(tenantId, new[] { name }, ct);
        return new RoleViewModel
        {
            Id = id, Name = name, DisplayName = display, DefaultName = defaultName, Description = description,
            IsBuiltIn = isBuiltIn, IsLocked = name == AppRoles.Owner, IsHidden = isHidden && name != AppRoles.Owner,
            Permissions = granted.ToList(), StaffCount = staffCount,
            Area = granted.Contains(Permissions.ProductionWork) && !granted.Contains(Permissions.OrdersList) ? "karigar" : "shop"
        };
    }

    private void AddGrants(string roleName, IEnumerable<string> permissions, DateTime now)
    {
        foreach (var p in permissions.Distinct())
        {
            _db.TenantRolePermissions.Add(new TenantRolePermission
            {
                TenantId = _currentUser.TenantId, RoleName = roleName, Permission = p, Granted = true,
                UpdatedOn = now, UpdatedBy = _currentUser.UserId
            });
        }
    }

    private static string? CheckPermissions(List<string> permissions)
    {
        var unknown = permissions.Where(p => !Permissions.IsKnown(p)).ToList();
        if (unknown.Count > 0) return $"Unknown permission(s): {string.Join(", ", unknown)}.";
        var ownerOnly = permissions.Where(p => Permissions.NonDelegable.Contains(p)).ToList();
        if (ownerOnly.Count > 0) return $"These belong to the Owner only: {string.Join(", ", ownerOnly)}.";
        return null;
    }

    private static string Clean(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string? CleanOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
