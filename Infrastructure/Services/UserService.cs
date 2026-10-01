using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.User;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class UserService : IUserService
{
    private static readonly string[] RestrictedRoles = { AppRoles.SystemAdmin, AppRoles.Owner };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<Identity.ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlanLimitService _limits;

    public UserService(ApplicationDbContext db, UserManager<Identity.ApplicationUser> userManager,
        ICurrentUserService currentUser, IPlanLimitService limits)
    {
        _db          = db;
        _userManager = userManager;
        _currentUser = currentUser;
        _limits      = limits;
    }

    public async Task<ApiResponse<List<RoleViewModel>>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles.AsNoTracking()
            .Where(r => r.Name != AppRoles.SystemAdmin)
            .OrderBy(r => r.Name)
            .Select(r => new RoleViewModel { Id = r.Id, Name = r.Name! })
            .ToListAsync(ct);
        return ApiResponse<List<RoleViewModel>>.Ok(roles);
    }

    public async Task<ApiResponse<PagedResult<UserListViewModel>>> GetListAsync(Guid? shopId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = _db.AppUsers.AsNoTracking().AsQueryable();
        if (shopId.HasValue) query = query.Where(u => u.ShopIds.Contains(shopId.Value));

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var roleNames = await RoleNameMapAsync(ct);

        var items = users.Select(u => new UserListViewModel
        {
            Id            = u.Id,
            Name          = u.Name,
            Email         = u.Email,
            Phone         = u.Phone,
            Roles         = Names(u.RoleIds, roleNames),
            ActiveStatus  = u.ActiveStatus,
            LastLoginDate = u.LastLoginDate
        }).ToList();

        return ApiResponse<PagedResult<UserListViewModel>>.Ok(PagedResult<UserListViewModel>.From(items, total, page, pageSize));
    }

    public async Task<ApiResponse<UserDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<UserDetailViewModel>.NotFound("User not found.");
        var roleNames = await RoleNameMapAsync(ct);

        return ApiResponse<UserDetailViewModel>.Ok(new UserDetailViewModel
        {
            Id            = user.Id,
            Name          = user.Name,
            Email         = user.Email,
            Phone         = user.Phone,
            RoleIds       = user.RoleIds,
            RoleNames     = Names(user.RoleIds, roleNames),
            ShopIds       = user.ShopIds,
            ActiveStatus  = user.ActiveStatus,
            LastLoginDate = user.LastLoginDate,
            LoginCount    = user.LoginCount,
            CreatedOn     = user.CreatedOn
        });
    }

    public async Task<ApiResponse<UserDetailViewModel>> CreateAsync(CreateUserViewModel vm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vm.Email) && string.IsNullOrWhiteSpace(vm.Phone))
            return ApiResponse<UserDetailViewModel>.Fail("Either email or phone is required.");
        var canLogIn = (!string.IsNullOrWhiteSpace(vm.Email) && !string.IsNullOrWhiteSpace(vm.Password))
                       || (!string.IsNullOrWhiteSpace(vm.Phone) && !string.IsNullOrWhiteSpace(vm.Passcode));
        if (!canLogIn)
            return ApiResponse<UserDetailViewModel>.Fail("Provide email + password, or phone + PIN, so the user can sign in.");

        var roleError = await ValidateRolesAsync(vm.RoleIds, ct);
        if (roleError != null) return roleError.As<UserDetailViewModel>();
        var shopError = await ValidateShopsAsync(vm.ShopIds, ct);
        if (shopError != null) return ApiResponse<UserDetailViewModel>.Fail(shopError);

        if (!string.IsNullOrWhiteSpace(vm.Phone) && await PhoneTakenAsync(vm.Phone, null, ct))
            return ApiResponse<UserDetailViewModel>.Conflict("An account with this phone number already exists.");

        var limit = await _limits.CheckAsync(PlanLimit.Users, ct);
        if (limit != null) return ApiResponse<UserDetailViewModel>.PaymentRequired(limit);

        var shopIds = vm.ShopIds.Count > 0 ? vm.ShopIds.Distinct().ToList() : new List<Guid> { _currentUser.ShopId };
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        Guid? authId = null;
        if (!string.IsNullOrWhiteSpace(vm.Email) && !string.IsNullOrWhiteSpace(vm.Password))
        {
            var aspUser = new Identity.ApplicationUser { UserName = vm.Email, Email = vm.Email };
            var result = await _userManager.CreateAsync(aspUser, vm.Password);
            if (!result.Succeeded)
                return ApiResponse<UserDetailViewModel>.Fail(result.Errors.Select(e => e.Description).ToList());

            var roles = await RoleNamesAsync(vm.RoleIds, ct);
            if (roles.Count > 0) await _userManager.AddToRolesAsync(aspUser, roles);
            authId = aspUser.Id;
        }

        var user = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = _currentUser.TenantId,
            BranchId     = shopIds.Contains(_currentUser.ShopId) ? _currentUser.ShopId : shopIds[0],
            AuthId       = authId,
            Name         = vm.Name.Trim(),
            Email        = vm.Email,
            Phone        = vm.Phone?.Trim(),
            Passcode     = !string.IsNullOrWhiteSpace(vm.Passcode) ? BCrypt.Net.BCrypt.HashPassword(vm.Passcode) : null,
            RoleIds      = vm.RoleIds,
            ShopIds      = shopIds,
            ActiveStatus = ActiveStatus.Active
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetDetailAsync(user.Id, ct);
    }

    /// <summary>
    /// Managers may edit a staff member's name and phone. Only the Owner may change roles, branch
    /// access or active status, and nobody here may grant Owner or SystemAdmin (previously a
    /// Manager could promote themselves to Owner through this endpoint).
    /// </summary>
    public async Task<ApiResponse<UserDetailViewModel>> UpdateAsync(UpdateUserViewModel vm, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == vm.Id, ct);
        if (user == null) return ApiResponse<UserDetailViewModel>.NotFound("User not found.");

        var roleNames = await RoleNameMapAsync(ct);
        var targetIsPrivileged = Names(user.RoleIds, roleNames).Any(r => RestrictedRoles.Contains(r));
        if (targetIsPrivileged && !_currentUser.IsOwner)
            return ApiResponse<UserDetailViewModel>.Forbidden("Only the Owner can edit an Owner account.");

        var changesAccess = vm.RoleIds != null || vm.ShopIds != null || vm.ActiveStatus.HasValue;
        if (changesAccess && !_currentUser.IsOwner)
            return ApiResponse<UserDetailViewModel>.Forbidden("Only the Owner can change roles, branch access or account status.");

        if (vm.RoleIds != null)
        {
            var roleError = await ValidateRolesAsync(vm.RoleIds, ct);
            if (roleError != null) return roleError.As<UserDetailViewModel>();
            if (user.Id == _currentUser.UserId)
                return ApiResponse<UserDetailViewModel>.Fail("You cannot change your own roles.");
        }
        if (vm.ShopIds != null)
        {
            var shopError = await ValidateShopsAsync(vm.ShopIds, ct);
            if (shopError != null) return ApiResponse<UserDetailViewModel>.Fail(shopError);
            if (vm.ShopIds.Count == 0) return ApiResponse<UserDetailViewModel>.Fail("A user needs access to at least one branch.");
        }
        if (vm.Phone != null && await PhoneTakenAsync(vm.Phone, user.Id, ct))
            return ApiResponse<UserDetailViewModel>.Conflict("An account with this phone number already exists.");
        if (vm.ActiveStatus == ActiveStatus.Inactive && user.Id == _currentUser.UserId)
            return ApiResponse<UserDetailViewModel>.Fail("You cannot deactivate your own account.");

        if (vm.Name != null) user.Name = vm.Name.Trim();
        if (vm.Phone != null) user.Phone = vm.Phone.Trim();
        if (vm.ShopIds != null) user.ShopIds = vm.ShopIds.Distinct().ToList();
        if (vm.ActiveStatus.HasValue) user.ActiveStatus = vm.ActiveStatus.Value;

        if (vm.RoleIds != null)
        {
            user.RoleIds = vm.RoleIds;
            await SyncIdentityRolesAsync(user, vm.RoleIds, ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(vm.Id, ct);
    }

    public async Task<ApiResponse<object>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        if (id == _currentUser.UserId) return ApiResponse<object>.Fail("You cannot deactivate your own account.");
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.NotFound("User not found.");

        user.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
        await RevokeSessionsAsync(user.Id, ct);

        if (user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
            {
                await _userManager.SetLockoutEnabledAsync(aspUser, true);
                await _userManager.SetLockoutEndDateAsync(aspUser, DateTimeOffset.MaxValue);
            }
        }
        return ApiResponse<object>.Ok(null, "User deactivated.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == _currentUser.UserId) return ApiResponse<object>.Fail("You cannot delete your own account.");
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.NotFound("User not found.");

        var roleNames = await RoleNameMapAsync(ct);
        if (Names(user.RoleIds, roleNames).Any(r => RestrictedRoles.Contains(r)))
            return ApiResponse<object>.Fail("Owner and SystemAdmin accounts cannot be deleted here. Use admin provisioning.");

        if (user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
            {
                await _userManager.RemoveFromRolesAsync(aspUser, await _userManager.GetRolesAsync(aspUser));
                await _userManager.SetLockoutEnabledAsync(aspUser, true);
                await _userManager.SetLockoutEndDateAsync(aspUser, DateTimeOffset.MaxValue);
            }
        }

        user.IsDeleted = true;
        user.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
        await RevokeSessionsAsync(user.Id, ct);
        return ApiResponse<object>.Ok(null, "User account deleted.");
    }

    public async Task<ApiResponse<object>> AssignShopsAsync(Guid id, List<Guid> shopIds, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.NotFound("User not found.");
        if (shopIds.Count == 0) return ApiResponse<object>.Fail("A user needs access to at least one branch.");
        var shopError = await ValidateShopsAsync(shopIds, ct);
        if (shopError != null) return ApiResponse<object>.Fail(shopError);

        user.ShopIds = shopIds.Distinct().ToList();
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, $"Assigned {user.ShopIds.Count} shop(s).");
    }

    public async Task<ApiResponse<object>> AssignRolesAsync(Guid id, List<Guid> roleIds, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.NotFound("User not found.");
        if (user.Id == _currentUser.UserId) return ApiResponse<object>.Fail("You cannot change your own roles.");
        var roleError = await ValidateRolesAsync(roleIds, ct);
        if (roleError != null) return roleError;

        user.RoleIds = roleIds;
        await SyncIdentityRolesAsync(user, roleIds, ct);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Roles updated.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<ApiResponse<object>?> ValidateRolesAsync(List<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0) return null;
        var names = await RoleNamesAsync(roleIds, ct);
        if (names.Count != roleIds.Distinct().Count())
            return ApiResponse<object>.NotFound("One or more roles were not found.");
        if (names.Any(r => RestrictedRoles.Contains(r)))
            return ApiResponse<object>.Forbidden($"Cannot assign '{AppRoles.Owner}' or '{AppRoles.SystemAdmin}' roles here. Use admin provisioning.");
        return null;
    }

    /// <summary>Every branch id must be a branch of the caller's tenant.</summary>
    private async Task<string?> ValidateShopsAsync(List<Guid> shopIds, CancellationToken ct)
    {
        var ids = shopIds.Distinct().ToList();
        if (ids.Count == 0) return null;
        var found = await _db.Shops.CountAsync(s => ids.Contains(s.Id), ct);   // tenant-filtered
        return found == ids.Count ? null : "One or more branches do not belong to your business.";
    }

    /// <summary>Phone numbers log in with a PIN across all tenants, so they must be unique platform-wide.</summary>
    private Task<bool> PhoneTakenAsync(string phone, Guid? excludeId, CancellationToken ct)
    {
        var trimmed = phone.Trim();
        return _db.AppUsers.IgnoreQueryFilters().AnyAsync(u => u.Phone == trimmed && !u.IsDeleted && (excludeId == null || u.Id != excludeId), ct);
    }

    private async Task SyncIdentityRolesAsync(User user, List<Guid> roleIds, CancellationToken ct)
    {
        if (!user.AuthId.HasValue) return;
        var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
        if (aspUser == null) return;
        await _userManager.RemoveFromRolesAsync(aspUser, await _userManager.GetRolesAsync(aspUser));
        var names = await RoleNamesAsync(roleIds, ct);
        if (names.Count > 0) await _userManager.AddToRolesAsync(aspUser, names);
    }

    /// <summary>Signs the user out everywhere by revoking their refresh tokens.</summary>
    private Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
        => _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);

    private async Task<List<string>> RoleNamesAsync(List<Guid> roleIds, CancellationToken ct)
        => await _db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name!).ToListAsync(ct);

    private async Task<Dictionary<Guid, string>> RoleNameMapAsync(CancellationToken ct)
        => await _db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, ct);

    private static List<string> Names(IEnumerable<Guid> roleIds, IReadOnlyDictionary<Guid, string> map)
        => roleIds.Select(id => map.GetValueOrDefault(id)).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList();
}
