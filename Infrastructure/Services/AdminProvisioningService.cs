using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Application.ViewModels.Shop;
using Application.ViewModels.User;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Handles all SystemAdmin provisioning operations:
///   Step 1: Provision Owner (user + identity account)
///   Step 2: Provision Shop (shop + link to owner)
///   Step 3: Provision Subscription (set trial/active status on shop)
/// Uses IgnoreQueryFilters() throughout since SystemAdmin operates across all tenants.
/// </summary>
public class AdminProvisioningService : IAdminProvisioningService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ITenantSetupService _tenantSetup;

    public AdminProvisioningService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ITenantSetupService tenantSetup)
    {
        _db          = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantSetup = tenantSetup;
    }

    // ── Step 1: Provision Owner ─────────────────────────────────────────────

    public async Task<ApiResponse<ProvisionOwnerResult>> ProvisionOwnerAsync(ProvisionOwnerRequest req, CancellationToken ct = default)
    {
        // Validate: must have either email+password OR phone+passcode
        bool hasEmail = !string.IsNullOrWhiteSpace(req.Email) && !string.IsNullOrWhiteSpace(req.Password);
        bool hasPhone = !string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.Passcode);

        if (!hasEmail && !hasPhone)
            return ApiResponse<ProvisionOwnerResult>.Fail("Provide either Email+Password or Phone+Passcode.");

        // Check duplicate email
        if (hasEmail)
        {
            var existing = await _userManager.FindByEmailAsync(req.Email!);
            if (existing != null)
                return ApiResponse<ProvisionOwnerResult>.Fail("An account with this email already exists.");
        }

        // Check duplicate phone
        if (hasPhone)
        {
            var phoneExists = await _db.AppUsers.IgnoreQueryFilters()
                .AnyAsync(u => u.Phone == req.Phone && !u.IsDeleted, ct);
            if (phoneExists)
                return ApiResponse<ProvisionOwnerResult>.Fail("An account with this phone number already exists.");
        }

        var tenantId = Guid.NewGuid();
        var now      = DateTime.UtcNow;
        Guid? authId = null;

        // 1a. Create ASP.NET Identity user (for email+password login)
        if (hasEmail)
        {
            var identityUser = new ApplicationUser
            {
                UserName       = req.Email,
                Email          = req.Email,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(identityUser, req.Password!);
            if (!result.Succeeded)
                return ApiResponse<ProvisionOwnerResult>.Fail(result.Errors.Select(e => e.Description).ToList());

            await _userManager.AddToRoleAsync(identityUser, AppRoles.Owner);
            authId = identityUser.Id;
        }

        // 1b. Get Owner role ID for domain user
        var ownerRole = await _roleManager.FindByNameAsync(AppRoles.Owner);
        var roleIds   = ownerRole != null ? new List<Guid> { ownerRole.Id } : new List<Guid>();

        // 1c. Create domain User record
        var domainUser = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            BranchId     = Guid.Empty,   // will be set when first shop is provisioned
            AuthId       = authId,
            Name         = req.Name,
            Email        = req.Email,
            Phone        = req.Phone,
            Passcode     = hasPhone ? BCrypt.Net.BCrypt.HashPassword(req.Passcode) : null,
            RoleIds      = roleIds,
            ShopIds      = new List<Guid>(),
            ActiveStatus = ActiveStatus.Active,
            CreatedOn    = now,
            UpdatedOn    = now
        };

        _db.AppUsers.Add(domainUser);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ProvisionOwnerResult>.Ok(new ProvisionOwnerResult
        {
            UserId   = domainUser.Id,
            TenantId = tenantId,
            Name     = domainUser.Name,
            Email    = domainUser.Email
        }, "Owner provisioned successfully.");
    }

    // ── Step 2: Provision Shop ──────────────────────────────────────────────

    public async Task<ApiResponse<ProvisionShopResult>> ProvisionShopAsync(ProvisionShopRequest req, CancellationToken ct = default)
    {
        // Load the owner (ignore tenant filter — admin crosses all tenants)
        var owner = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == req.UserId && !u.IsDeleted, ct);

        if (owner == null)
            return ApiResponse<ProvisionShopResult>.Fail("Owner user not found. Provision the owner first (Step 1).");

        var now = DateTime.UtcNow;

        // Wrap everything in a transaction — if TenantSetup seeding fails,
        // the shop and user changes are rolled back so data stays consistent.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Create the shop under the owner's TenantId
            var shop = new Shop
            {
                Id             = Guid.NewGuid(),
                TenantId       = owner.TenantId,
                OwnerId        = owner.Id,
                Name           = req.ShopName,
                Address        = req.Address,
                City           = req.City,
                Phone          = req.Phone,
                WhatsAppNumber = req.WhatsAppNumber,
                LogoUrl        = req.LogoUrl,
                Currency       = req.Currency,
                CurrencySymbol = req.CurrencySymbol,
                ActiveStatus   = ActiveStatus.Active,
                Status         = SubscriptionStatus.Trial,
                TrialEndsAt    = now.AddDays(14),
                CreatedBy      = owner.Id,
                CreatedOn      = now,
                UpdatedBy      = owner.Id,
                UpdatedOn      = now
            };
            // BranchId = Shop.Id — the key rule from MobilePosApi
            shop.BranchId = shop.Id;

            _db.Shops.Add(shop);

            // Link shop to owner
            if (owner.ShopIds.Count == 0)
                owner.BranchId = shop.Id;  // first shop becomes the home branch

            owner.ShopIds  = owner.ShopIds.Append(shop.Id).ToList();
            owner.UpdatedOn = now;

            await _db.SaveChangesAsync(ct);

            // Seed measurement fields + templates — still inside the transaction
            await _tenantSetup.SetupNewTenantAsync(owner.TenantId, shop.Id, owner.Id, ct);

            await tx.CommitAsync(ct);

            return ApiResponse<ProvisionShopResult>.Ok(new ProvisionShopResult
            {
                ShopId   = shop.Id,
                TenantId = owner.TenantId,
                ShopName = shop.Name
            }, "Shop provisioned and linked to owner.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Step 3: Provision Subscription ─────────────────────────────────────

    public async Task<ApiResponse<bool>> ProvisionSubscriptionAsync(ProvisionSubscriptionRequest req, CancellationToken ct = default)
    {
        var shop = await _db.Shops.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == req.ShopId && !s.IsDeleted, ct);

        if (shop == null)
            return ApiResponse<bool>.Fail("Shop not found.");

        shop.Status              = req.Status;
        shop.SubscriptionEndsAt  = req.Status == SubscriptionStatus.Active ? req.SubscriptionEndsAt : null;
        shop.TrialEndsAt         = req.Status == SubscriptionStatus.Trial ? DateTime.UtcNow.AddDays(req.TrialDays) : shop.TrialEndsAt;
        shop.SubscriptionPlanName = req.PlanName;
        shop.UpdatedOn           = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Subscription set to '{req.Status}' for shop '{shop.Name}'.");
    }

    // ── Subscription Management ─────────────────────────────────────────────

    public async Task<ApiResponse<bool>> ActivateSubscriptionAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == shopId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<bool>.Fail("Shop not found.");

        shop.Status    = SubscriptionStatus.Active;
        shop.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Subscription activated.");
    }

    public async Task<ApiResponse<bool>> SuspendSubscriptionAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == shopId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<bool>.Fail("Shop not found.");

        shop.Status    = SubscriptionStatus.Suspended;
        shop.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Subscription suspended.");
    }

    public async Task<ApiResponse<bool>> ExtendSubscriptionAsync(Guid shopId, int days, CancellationToken ct = default)
    {
        var shop = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == shopId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<bool>.Fail("Shop not found.");

        if (shop.Status == SubscriptionStatus.Trial)
            shop.TrialEndsAt = (shop.TrialEndsAt ?? DateTime.UtcNow).AddDays(days);
        else
            shop.SubscriptionEndsAt = (shop.SubscriptionEndsAt ?? DateTime.UtcNow).AddDays(days);

        shop.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Extended by {days} day(s).");
    }

    // ── User Role Management ────────────────────────────────────────────────

    public async Task<ApiResponse<bool>> UpdateUserRoleAsync(Guid userId, string newRole, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (domainUser == null) return ApiResponse<bool>.Fail("User not found.");

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(newRole))
            return ApiResponse<bool>.Fail($"Role '{newRole}' does not exist.");

        var role = await _roleManager.FindByNameAsync(newRole);

        // Update domain user role IDs
        domainUser.RoleIds  = role != null ? new List<Guid> { role.Id } : new List<Guid>();
        domainUser.UpdatedOn = DateTime.UtcNow;

        // Sync with Identity if email+password user
        if (domainUser.AuthId.HasValue)
        {
            var identityUser = await _userManager.FindByIdAsync(domainUser.AuthId.Value.ToString());
            if (identityUser != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(identityUser);
                await _userManager.RemoveFromRolesAsync(identityUser, currentRoles);
                await _userManager.AddToRoleAsync(identityUser, newRole);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Role updated to '{newRole}'.");
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<UserListViewModel>>> GetOwnersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        // Find the Owner role
        var ownerRole = await _roleManager.FindByNameAsync(AppRoles.Owner);
        if (ownerRole == null)
            return ApiResponse<PagedResult<UserListViewModel>>.Ok(PagedResult<UserListViewModel>.From(new List<UserListViewModel>(), 0, page, pageSize));

        var query = _db.AppUsers.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.RoleIds.Contains(ownerRole.Id))
            .OrderBy(u => u.Name);

        var total = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = users.Select(u => new UserListViewModel
        {
            Id           = u.Id,
            Name         = u.Name,
            Email        = u.Email,
            Phone        = u.Phone,
            Roles        = new List<string> { AppRoles.Owner },
            ActiveStatus = u.ActiveStatus,
            LastLoginDate = u.LastLoginDate
        }).ToList();

        return ApiResponse<PagedResult<UserListViewModel>>.Ok(PagedResult<UserListViewModel>.From(items, total, page, pageSize));
    }

    public async Task<ApiResponse<PagedResult<ShopListViewModel>>> GetAllShopsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Shops.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name);

        var total = await query.CountAsync(ct);
        var shops = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = shops.Select(s => new ShopListViewModel
        {
            Id           = s.Id,
            Name         = s.Name,
            City         = s.City,
            Phone        = s.Phone,
            ActiveStatus = s.ActiveStatus,
            CreatedOn    = s.CreatedOn
        }).ToList();

        return ApiResponse<PagedResult<ShopListViewModel>>.Ok(PagedResult<ShopListViewModel>.From(items, total, page, pageSize));
    }
}
