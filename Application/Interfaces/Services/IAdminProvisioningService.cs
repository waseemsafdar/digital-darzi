using Application.Common;
using Application.ViewModels.Admin;
using Application.ViewModels.Shop;
using Application.ViewModels.User;

namespace Application.Interfaces.Services;

/// <summary>
/// Admin-only service for provisioning owners, shops, and subscriptions.
/// Only SystemAdmin accounts should call these methods.
/// </summary>
public interface IAdminProvisioningService
{
    // ── 3-Step Provisioning ─────────────────────────────────────────────────

    /// <summary>Step 1: Create an Owner user + Identity account.</summary>
    Task<ApiResponse<ProvisionOwnerResult>> ProvisionOwnerAsync(ProvisionOwnerRequest req, CancellationToken ct = default);

    /// <summary>Step 2: Create a Shop and link it to an existing owner.</summary>
    Task<ApiResponse<ProvisionShopResult>> ProvisionShopAsync(ProvisionShopRequest req, CancellationToken ct = default);

    /// <summary>Step 3: Assign or update a subscription for a shop.</summary>
    Task<ApiResponse<bool>> ProvisionSubscriptionAsync(ProvisionSubscriptionRequest req, CancellationToken ct = default);

    // ── Subscription Management ─────────────────────────────────────────────

    Task<ApiResponse<bool>> ActivateSubscriptionAsync(Guid shopId, CancellationToken ct = default);
    Task<ApiResponse<bool>> SuspendSubscriptionAsync(Guid shopId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ExtendSubscriptionAsync(Guid shopId, int days, CancellationToken ct = default);

    // ── User Role Management ────────────────────────────────────────────────

    Task<ApiResponse<bool>> UpdateUserRoleAsync(Guid userId, string newRole, CancellationToken ct = default);

    // ── Queries ─────────────────────────────────────────────────────────────

    Task<ApiResponse<PagedResult<UserListViewModel>>> GetOwnersAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<ShopListViewModel>>> GetAllShopsAsync(int page, int pageSize, CancellationToken ct = default);
}
