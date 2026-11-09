using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class SignupRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
}

public sealed class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShopName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .WithMessage("Use at least 8 characters for the password.");
        RuleFor(x => x.Phone).Matches(@"^\+?\d{10,15}$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter the phone number with digits only, e.g. 03001234567.");
        RuleFor(x => x.City).MaximumLength(80);
    }
}

public interface ISignupService
{
    /// <summary>Creates the business, its first shop and the Owner, starts the trial and signs the Owner in.</summary>
    Task<ApiResponse<AuthTokenResponse>> RegisterAsync(SignupRequest req, CancellationToken ct = default);
}

/// <summary>
/// Self-service sign-up (2026-11). Uses the same two steps as the admin console, so a shop that
/// signs up gets exactly what an admin-created shop gets (starter styles, options, categories).
/// There is no phone or email check yet: one account per email and per phone is the only guard.
/// </summary>
public sealed class SignupService : ISignupService
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminProvisioningService _provisioning;
    private readonly IAuthService _auth;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IShopSettingsCache _shopCache;
    private readonly BillingOptions _billing;

    public SignupService(ApplicationDbContext db, IAdminProvisioningService provisioning, IAuthService auth,
        UserManager<ApplicationUser> userManager, IShopSettingsCache shopCache, IOptions<BillingOptions> billing)
    {
        _db = db;
        _provisioning = provisioning;
        _auth = auth;
        _userManager = userManager;
        _shopCache = shopCache;
        _billing = billing.Value;
    }

    public async Task<ApiResponse<AuthTokenResponse>> RegisterAsync(SignupRequest req, CancellationToken ct = default)
    {
        if (!_billing.SignupEnabled) return ApiResponse<AuthTokenResponse>.Forbidden("Sign-up is closed. Contact us to open a shop.");

        var email = req.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        if (await _userManager.FindByEmailAsync(email) != null)
            return ApiResponse<AuthTokenResponse>.Conflict("An account with this email already exists. Sign in instead.");
        if (phone != null && await _db.AppUsers.IgnoreQueryFilters().AnyAsync(u => u.Phone == phone && !u.IsDeleted, ct))
            return ApiResponse<AuthTokenResponse>.Conflict("An account with this phone number already exists.");

        var owner = await _provisioning.ProvisionOwnerAsync(new ProvisionOwnerRequest
        {
            Name = req.Name.Trim(), Email = email, Password = req.Password, Phone = phone
        }, ct);
        if (!owner.Success || owner.Data == null)
            return ApiResponse<AuthTokenResponse>.Fail(owner.Errors.Count > 0 ? owner.Errors : new List<string> { owner.Message ?? "Could not create the account." });

        try
        {
            var shop = await _provisioning.ProvisionShopAsync(new ProvisionShopRequest
            {
                UserId = owner.Data.UserId, ShopName = req.ShopName.Trim(), City = req.City?.Trim(), Phone = phone,
                WhatsAppNumber = phone
            }, ct);
            if (!shop.Success || shop.Data == null)
            {
                await RemoveOwnerAsync(owner.Data.UserId, ct);
                return ApiResponse<AuthTokenResponse>.Fail(shop.Errors.Count > 0 ? shop.Errors : new List<string> { "Could not create the shop." });
            }

            await StartTrialAsync(shop.Data.ShopId, ct);
        }
        catch
        {
            await RemoveOwnerAsync(owner.Data.UserId, ct);
            throw;
        }

        return await _auth.LoginAsync(new AuthLoginRequest(email, null, req.Password, null), ct);
    }

    /// <summary>Puts the new shop on the trial plan for that plan's trial length.</summary>
    private async Task StartTrialAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await _db.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == shopId, ct);
        var plan = await _db.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == _billing.TrialPlanName && p.IsActive, ct);
        var days = plan?.TrialDays > 0 ? plan.TrialDays : _billing.DefaultTrialDays;

        shop.Status = SubscriptionStatus.Trial;
        shop.TrialEndsAt = DateTime.UtcNow.AddDays(days);
        if (plan != null)
        {
            shop.SubscriptionPlanId = plan.Id;
            shop.SubscriptionPlanName = plan.Name;
        }
        await _db.SaveChangesAsync(ct);
        _shopCache.Invalidate(shop.Id);
        _shopCache.InvalidateTenant(shop.TenantId);
    }

    /// <summary>Undo a half-finished sign-up so the email can be used again.</summary>
    private async Task RemoveOwnerAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return;
        if (user.AuthId.HasValue)
        {
            var identity = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (identity != null) await _userManager.DeleteAsync(identity);
        }
        _db.AppUsers.Remove(user);
        await _db.SaveChangesAsync(ct);
    }
}
