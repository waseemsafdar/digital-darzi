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
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Identity.ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public UserService(
        ApplicationDbContext db,
        UserManager<Identity.ApplicationUser> userManager,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<UserListViewModel>>> GetListAsync(Guid? shopId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AppUsers.AsNoTracking().AsQueryable();
        if (shopId.HasValue)
            query = query.Where(u => u.ShopIds.Contains(shopId.Value));

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Fetch role names from Identity
        var authIds = users.Where(u => u.AuthId.HasValue).Select(u => u.AuthId!.Value).ToList();
        var roleMap = new Dictionary<Guid, List<string>>();

        foreach (var u in users.Where(u => u.AuthId.HasValue))
        {
            var aspUser = await _userManager.FindByIdAsync(u.AuthId!.Value.ToString());
            if (aspUser != null)
            {
                var roles = (await _userManager.GetRolesAsync(aspUser)).ToList();
                roleMap[u.AuthId!.Value] = roles;
            }
        }

        var items = users.Select(u => new UserListViewModel
        {
            Id            = u.Id,
            Name          = u.Name,
            Email         = u.Email,
            Phone         = u.Phone,
            Roles         = u.AuthId.HasValue && roleMap.ContainsKey(u.AuthId.Value)
                                ? roleMap[u.AuthId.Value]
                                : new List<string>(),
            ActiveStatus  = u.ActiveStatus,
            LastLoginDate = u.LastLoginDate
        }).ToList();

        return ApiResponse<PagedResult<UserListViewModel>>.Ok(
            PagedResult<UserListViewModel>.From(items, total, page, pageSize));
    }

    public async Task<ApiResponse<UserDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<UserDetailViewModel>.Fail("User not found.");

        var roleNames = new List<string>();
        if (user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
                roleNames = (await _userManager.GetRolesAsync(aspUser)).ToList();
        }

        return ApiResponse<UserDetailViewModel>.Ok(new UserDetailViewModel
        {
            Id            = user.Id,
            Name          = user.Name,
            Email         = user.Email,
            Phone         = user.Phone,
            RoleIds       = user.RoleIds,
            RoleNames     = roleNames,
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

        // Guard: Owner/SystemAdmin can only be provisioned through /api/admin/provision-owner.
        // Reject any attempt to create a staff member with an elevated role.
        if (vm.RoleIds.Any())
        {
            var requestedRoleNames = await _db.Roles
                .Where(r => vm.RoleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync(ct);

            var restricted = new[] { AppRoles.SystemAdmin, AppRoles.Owner };
            if (requestedRoleNames.Any(r => restricted.Contains(r)))
                return ApiResponse<UserDetailViewModel>.Fail(
                    $"Cannot assign '{AppRoles.Owner}' or '{AppRoles.SystemAdmin}' roles here. Use admin provisioning.");
        }

        var now = DateTime.UtcNow;
        Guid? authId = null;

        // Create Identity user if email+password provided
        if (!string.IsNullOrWhiteSpace(vm.Email) && !string.IsNullOrWhiteSpace(vm.Password))
        {
            var aspUser = new Identity.ApplicationUser
            {
                UserName = vm.Email,
                Email    = vm.Email
            };
            var result = await _userManager.CreateAsync(aspUser, vm.Password);
            if (!result.Succeeded)
                return ApiResponse<UserDetailViewModel>.Fail(result.Errors.Select(e => e.Description).ToList());

            // Assign roles
            if (vm.RoleIds.Any())
            {
                var roles = await _db.Roles
                    .Where(r => vm.RoleIds.Contains(r.Id))
                    .Select(r => r.Name!)
                    .ToListAsync(ct);
                await _userManager.AddToRolesAsync(aspUser, roles);
            }

            authId = aspUser.Id;
        }

        var user = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = _currentUser.TenantId,
            BranchId     = _currentUser.ShopId,
            AuthId       = authId,
            Name         = vm.Name,
            Email        = vm.Email,
            Phone        = vm.Phone,
            Passcode     = !string.IsNullOrWhiteSpace(vm.Passcode)
                               ? BCrypt.Net.BCrypt.HashPassword(vm.Passcode)
                               : null,
            RoleIds      = vm.RoleIds,
            ShopIds      = vm.ShopIds,
            ActiveStatus = ActiveStatus.Active,
            CreatedBy    = _currentUser.UserId,
            CreatedOn    = now,
            UpdatedBy    = _currentUser.UserId,
            UpdatedOn    = now
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(user.Id, ct);
    }

    public async Task<ApiResponse<UserDetailViewModel>> UpdateAsync(UpdateUserViewModel vm, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == vm.Id, ct);
        if (user == null) return ApiResponse<UserDetailViewModel>.Fail("User not found.");

        if (vm.Name != null) user.Name = vm.Name;
        if (vm.Phone != null) user.Phone = vm.Phone;
        if (vm.RoleIds != null) user.RoleIds = vm.RoleIds;
        if (vm.ShopIds != null) user.ShopIds = vm.ShopIds;
        if (vm.ActiveStatus.HasValue) user.ActiveStatus = vm.ActiveStatus.Value;

        user.UpdatedBy = _currentUser.UserId;
        user.UpdatedOn = DateTime.UtcNow;

        // Sync Identity roles if updated
        if (vm.RoleIds != null && user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(aspUser);
                await _userManager.RemoveFromRolesAsync(aspUser, currentRoles);

                var newRoles = await _db.Roles
                    .Where(r => vm.RoleIds.Contains(r.Id))
                    .Select(r => r.Name!)
                    .ToListAsync(ct);
                await _userManager.AddToRolesAsync(aspUser, newRoles);
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(vm.Id, ct);
    }

    public async Task<ApiResponse<object>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.Fail("User not found.");

        user.ActiveStatus = ActiveStatus.Inactive;
        user.UpdatedBy    = _currentUser.UserId;
        user.UpdatedOn    = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Lock Identity account too
        if (user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
                await _userManager.SetLockoutEnabledAsync(aspUser, true);
        }

        return ApiResponse<object>.Ok((object?)null, "User deactivated.");
    }

    public async Task<ApiResponse<object>> AssignShopsAsync(Guid id, List<Guid> shopIds, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.Fail("User not found.");

        user.ShopIds  = shopIds;
        user.UpdatedBy = _currentUser.UserId;
        user.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok((object?)null, $"Assigned {shopIds.Count} shop(s).");
    }

    public async Task<ApiResponse<object>> AssignRolesAsync(Guid id, List<Guid> roleIds, CancellationToken ct = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return ApiResponse<object>.Fail("User not found.");

        // Guard: prevent escalating a staff member to Owner or SystemAdmin.
        // Those roles are managed exclusively via /api/admin/.
        if (roleIds.Any())
        {
            var requestedRoleNames = await _db.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync(ct);

            var restricted = new[] { AppRoles.SystemAdmin, AppRoles.Owner };
            if (requestedRoleNames.Any(r => restricted.Contains(r)))
                return ApiResponse<object>.Fail(
                    $"Cannot assign '{AppRoles.Owner}' or '{AppRoles.SystemAdmin}' roles here. Use admin provisioning.");
        }

        user.RoleIds   = roleIds;
        user.UpdatedBy = _currentUser.UserId;
        user.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (user.AuthId.HasValue)
        {
            var aspUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (aspUser != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(aspUser);
                await _userManager.RemoveFromRolesAsync(aspUser, currentRoles);

                var newRoles = await _db.Roles
                    .Where(r => roleIds.Contains(r.Id))
                    .Select(r => r.Name!)
                    .ToListAsync(ct);
                await _userManager.AddToRolesAsync(aspUser, newRoles);
            }
        }

        return ApiResponse<object>.Ok((object?)null, "Roles updated.");
    }
}
