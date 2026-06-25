using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Admin-only controller for provisioning owners, shops, and subscriptions.
/// All endpoints require the SystemAdmin role.
///
/// Typical 3-step flow:
///   1. POST /api/admin/provision-owner      → create owner user
///   2. POST /api/admin/provision-shop       → create shop + link to owner
///   3. POST /api/admin/provision-subscription → set trial/active status
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SystemAdmin")]
public class AdminProvisioningController : BaseController
{
    private readonly IAdminProvisioningService _adminService;

    public AdminProvisioningController(IAdminProvisioningService adminService)
    {
        _adminService = adminService;
    }

    // ── Step 1: Provision Owner ─────────────────────────────────────────────

    /// <summary>
    /// Creates a new Owner user and their Identity account.
    /// Provide either Email+Password (standard login) or Phone+Passcode (PIN login).
    /// Returns the UserId and TenantId needed for Step 2.
    /// </summary>
    [HttpPost("provision-owner")]
    public async Task<IActionResult> ProvisionOwner([FromBody] ProvisionOwnerRequest request, CancellationToken ct)
    {
        var result = await _adminService.ProvisionOwnerAsync(request, ct);
        return ReturnProcessedResponse(result);
    }

    // ── Step 2: Provision Shop ──────────────────────────────────────────────

    /// <summary>
    /// Creates a shop and links it to an existing owner user.
    /// The shop's BranchId is set equal to its own Id (BranchId = ShopId).
    /// Automatically seeds measurement fields and templates for the tenant.
    /// Returns the ShopId needed for Step 3.
    /// </summary>
    [HttpPost("provision-shop")]
    public async Task<IActionResult> ProvisionShop([FromBody] ProvisionShopRequest request, CancellationToken ct)
    {
        var result = await _adminService.ProvisionShopAsync(request, ct);
        return ReturnProcessedResponse(result);
    }

    // ── Step 3: Provision Subscription ─────────────────────────────────────

    /// <summary>
    /// Sets the subscription status for a shop (Trial, Active, Suspended, Cancelled).
    /// </summary>
    [HttpPost("provision-subscription")]
    public async Task<IActionResult> ProvisionSubscription([FromBody] ProvisionSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _adminService.ProvisionSubscriptionAsync(request, ct);
        return ReturnProcessedResponse(result);
    }

    // ── Subscription Management ─────────────────────────────────────────────

    /// <summary>Sets a shop's subscription status to Active.</summary>
    [HttpPost("subscription/activate/{shopId:guid}")]
    public async Task<IActionResult> ActivateSubscription(Guid shopId, CancellationToken ct)
        => ReturnProcessedResponse(await _adminService.ActivateSubscriptionAsync(shopId, ct));

    /// <summary>Suspends a shop's subscription (blocks all non-admin API access).</summary>
    [HttpPost("subscription/suspend/{shopId:guid}")]
    public async Task<IActionResult> SuspendSubscription(Guid shopId, CancellationToken ct)
        => ReturnProcessedResponse(await _adminService.SuspendSubscriptionAsync(shopId, ct));

    /// <summary>Extends a shop's trial or subscription end date by a number of days.</summary>
    [HttpPost("subscription/extend/{shopId:guid}")]
    public async Task<IActionResult> ExtendSubscription(Guid shopId, [FromBody] ExtendSubscriptionRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _adminService.ExtendSubscriptionAsync(shopId, request.Days, ct));

    // ── User Role Management ────────────────────────────────────────────────

    /// <summary>Updates the role of any user in the system.</summary>
    [HttpPut("user-role/{userId:guid}")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _adminService.UpdateUserRoleAsync(userId, request.NewRole, ct));

    // ── Queries ─────────────────────────────────────────────────────────────

    /// <summary>Lists all Owner accounts across all tenants (paginated).</summary>
    [HttpGet("owners")]
    public async Task<IActionResult> GetOwners(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _adminService.GetOwnersAsync(page, pageSize, ct));

    /// <summary>Lists all shops across all tenants (paginated).</summary>
    [HttpGet("shops")]
    public async Task<IActionResult> GetAllShops(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _adminService.GetAllShopsAsync(page, pageSize, ct));
}
