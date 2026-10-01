using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

/// <summary>
/// Sign-in and session management.
/// Access tokens are short-lived JWTs; refresh tokens are random 64-byte values stored only as a
/// SHA-256 hash, rotated on every use, and revoked as a family if an already-used token is
/// presented again (a sign of theft). Five failed sign-ins lock an account for 15 minutes.
/// </summary>
public class AuthService : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;
    private readonly IShopSettingsCache _shops;
    private readonly TimeProvider _clock;

    public AuthService(UserManager<ApplicationUser> userManager, ApplicationDbContext db, IConfiguration config,
        ICurrentUserService currentUser, INotificationService notifications, IPermissionService permissions,
        IShopSettingsCache shops, TimeProvider clock)
    {
        _userManager   = userManager;
        _db            = db;
        _config        = config;
        _currentUser   = currentUser;
        _notifications = notifications;
        _permissions   = permissions;
        _shops         = shops;
        _clock         = clock;
    }

    // ── Current session ────────────────────────────────────────────────────
    /// <summary>
    /// Roles and permissions come from the token the caller presented — the same claims every
    /// other endpoint authorises against — so the client never shows an action the API would refuse.
    /// </summary>
    public async Task<ApiResponse<MeResponse>> GetMeAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        var user = await _db.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user == null || user.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<MeResponse>.Fail("Account is inactive.", 401);

        var roles = _currentUser.Roles.ToList();
        var permissions = await _permissions.GetEffectivePermissionsAsync(user.TenantId, roles, ct);

        var branches = await _db.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => user.ShopIds.Contains(s.Id) && s.TenantId == user.TenantId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new BranchSummary(s.Id, s.Name, s.City))
            .ToListAsync(ct);

        var shop = await _shops.GetAsync(_currentUser.ShopId, ct);
        var governing = await _shops.GetGoverningSubscriptionAsync(user.TenantId, _currentUser.ShopId, ct);
        var subscription = governing == null ? null : new SubscriptionSummary(
            governing.Status, governing.TrialEndsAt, governing.SubscriptionEndsAt,
            await _db.Shops.IgnoreQueryFilters().Where(s => s.Id == governing.ShopId).Select(s => s.SubscriptionPlanName).FirstOrDefaultAsync(ct));

        return ApiResponse<MeResponse>.Ok(new MeResponse(
            user.Id, user.Name, user.Email, user.Phone, user.TenantId, _currentUser.ShopId, shop?.Name,
            shop?.Currency ?? "PKR", shop?.CurrencySymbol ?? "₨", roles, permissions, branches, subscription));
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ── Login ──────────────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> LoginAsync(AuthLoginRequest req, CancellationToken ct = default)
    {
        User? domainUser;

        if (!string.IsNullOrWhiteSpace(req.Email) && !string.IsNullOrWhiteSpace(req.Password))
        {
            var appUser = await _userManager.FindByEmailAsync(req.Email.Trim());
            if (appUser == null) return Invalid();
            if (await _userManager.IsLockedOutAsync(appUser)) return Locked();

            if (!await _userManager.CheckPasswordAsync(appUser, req.Password))
            {
                await _userManager.AccessFailedAsync(appUser);
                return await _userManager.IsLockedOutAsync(appUser) ? Locked() : Invalid();
            }
            await _userManager.ResetAccessFailedCountAsync(appUser);

            domainUser = await _db.AppUsers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.AuthId == appUser.Id && !u.IsDeleted, ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.Passcode))
        {
            var phone = req.Phone.Trim();
            var candidates = await _db.AppUsers.IgnoreQueryFilters()
                .Where(u => u.Phone == phone && !u.IsDeleted && u.Passcode != null)
                .ToListAsync(ct);
            if (candidates.Count == 0) return Invalid();
            if (candidates.All(u => u.LockoutEndsAt > Now)) return Locked();

            domainUser = candidates.FirstOrDefault(u => !(u.LockoutEndsAt > Now) && BCrypt.Net.BCrypt.Verify(req.Passcode, u.Passcode));
            if (domainUser == null)
            {
                foreach (var u in candidates.Where(u => !(u.LockoutEndsAt > Now)))
                {
                    u.FailedLoginCount++;
                    if (u.FailedLoginCount >= MaxFailedAttempts)
                    {
                        u.LockoutEndsAt = Now.Add(LockoutDuration);
                        u.FailedLoginCount = 0;
                    }
                }
                await _db.SaveChangesAsync(ct);
                return candidates.All(u => u.LockoutEndsAt > Now) ? Locked() : Invalid();
            }
            domainUser.FailedLoginCount = 0;
            domainUser.LockoutEndsAt = null;
        }
        else
        {
            return ApiResponse<AuthTokenResponse>.Fail("Provide email+password or phone+PIN.", 401);
        }

        if (domainUser == null || domainUser.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<AuthTokenResponse>.Fail("Account is inactive.", 401);

        domainUser.LastLoginDate = Now;
        domainUser.LoginCount++;

        var shopId = await DefaultShopAsync(domainUser, ct);
        var response = await IssueTokensAsync(domainUser, shopId, ct);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AuthTokenResponse>.Ok(response);
    }

    // ── Refresh / logout ───────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return ApiResponse<AuthTokenResponse>.Fail("Refresh token is required.", 401);

        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored == null) return ApiResponse<AuthTokenResponse>.Fail("Invalid refresh token.", 401);

        if (stored.RevokedAt != null)
        {
            // A rotated token came back: assume it was stolen and end every session for this user.
            await RevokeAllAsync(stored.UserId, ct);
            return ApiResponse<AuthTokenResponse>.Fail("Refresh token was already used. Please sign in again.", 401);
        }
        if (stored.ExpiresAt <= Now) return ApiResponse<AuthTokenResponse>.Fail("Refresh token expired. Please sign in again.", 401);

        var user = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == stored.UserId && !u.IsDeleted, ct);
        if (user == null || user.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<AuthTokenResponse>.Fail("Account is inactive.", 401);

        var shopId = user.ShopIds.Contains(stored.ShopId) ? stored.ShopId : await DefaultShopAsync(user, ct);
        var response = await IssueTokensAsync(user, shopId, ct, stored);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AuthTokenResponse>.Ok(response);
    }

    public async Task<ApiResponse<object>> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct = default)
    {
        if (request.AllDevices)
        {
            await RevokeAllAsync(userId, ct);
            return ApiResponse<object>.Ok(null, "Signed out on all devices.");
        }
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = Hash(request.RefreshToken);
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
            if (token != null && token.RevokedAt == null)
            {
                token.RevokedAt = Now;
                await _db.SaveChangesAsync(ct);
            }
        }
        return ApiResponse<object>.Ok(null, "Signed out.");
    }

    // ── Switch Branch ──────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> SwitchBranchAsync(Guid userId, Guid shopId, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (domainUser == null || domainUser.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<AuthTokenResponse>.Fail("User not found or inactive.", 401);

        if (!domainUser.ShopIds.Contains(shopId))
            return ApiResponse<AuthTokenResponse>.Forbidden("You do not have access to this branch.");

        // The branch must belong to the user's own tenant; a foreign id in ShopIds is never honoured.
        var shop = await _db.Shops.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == shopId && s.TenantId == domainUser.TenantId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<AuthTokenResponse>.NotFound("Branch not found.");

        var response = await IssueTokensAsync(domainUser, shopId, ct);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AuthTokenResponse>.Ok(response, $"Switched to branch '{shop.Name}'.");
    }

    // ── Credentials ────────────────────────────────────────────────────────
    public async Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, string current, string newPwd, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (domainUser?.AuthId == null) return ApiResponse<object>.NotFound("User not found.");
        var appUser = await _userManager.FindByIdAsync(domainUser.AuthId.ToString()!);
        if (appUser == null) return ApiResponse<object>.NotFound("Auth user not found.");
        var result = await _userManager.ChangePasswordAsync(appUser, current, newPwd);
        return result.Succeeded
            ? ApiResponse<object>.Ok(null, "Password changed.")
            : ApiResponse<object>.Fail(result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<ApiResponse<object>> ChangePinAsync(Guid userId, string current, string newPin, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (domainUser?.Passcode == null) return ApiResponse<object>.NotFound("User not found or not a PIN user.");
        if (!BCrypt.Net.BCrypt.Verify(current, domainUser.Passcode))
            return ApiResponse<object>.Fail("Current PIN is incorrect.");
        domainUser.Passcode = BCrypt.Net.BCrypt.HashPassword(newPin);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "PIN changed.");
    }

    /// <summary>
    /// Sends a 6-digit reset code by SMS (phone users) or email. The response is identical whether
    /// or not the account exists, so the endpoint cannot be used to discover accounts.
    /// </summary>
    public async Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        const string generic = "If an account exists for these details, a reset code has been sent.";
        var user = await FindForResetAsync(request.Email, request.Phone, ct);
        if (user == null || user.ActiveStatus == ActiveStatus.Inactive) return ApiResponse<object>.Ok(null, generic);

        await _db.PasswordResetCodes.Where(c => c.UserId == user.Id && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, Now), ct);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id, CodeHash = Hash($"{user.Id}:{code}"), CreatedOn = Now, ExpiresAt = Now.Add(ResetCodeLifetime)
        });

        var body = NotificationTemplates.Render(NotificationTemplates.Defaults[NotificationEvent.PasswordReset].Body,
            new Dictionary<string, string?> { ["Code"] = code });
        var byPhone = !string.IsNullOrWhiteSpace(request.Phone) && !string.IsNullOrWhiteSpace(user.Phone);
        _notifications.EnqueueDirect(user.TenantId, user.BranchId,
            byPhone ? NotificationChannel.Sms : NotificationChannel.Email,
            byPhone ? user.Phone! : user.Email!, body, NotificationEvent.PasswordReset);

        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, generic);
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        const string invalid = "The code is invalid or has expired.";
        var user = await FindForResetAsync(request.Email, request.Phone, ct);
        if (user == null) return ApiResponse<object>.Fail(invalid);

        var code = await _db.PasswordResetCodes.Where(c => c.UserId == user.Id && c.UsedAt == null && c.ExpiresAt > Now)
            .OrderByDescending(c => c.CreatedOn).FirstOrDefaultAsync(ct);
        if (code == null || code.Attempts >= MaxFailedAttempts) return ApiResponse<object>.Fail(invalid);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code.CodeHash), Encoding.UTF8.GetBytes(Hash($"{user.Id}:{request.Code?.Trim()}"))))
        {
            code.Attempts++;
            await _db.SaveChangesAsync(ct);
            return ApiResponse<object>.Fail(invalid);
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (user.AuthId == null) return ApiResponse<object>.Fail("This account signs in with a PIN. Send newPin instead.");
            var appUser = await _userManager.FindByIdAsync(user.AuthId.Value.ToString());
            if (appUser == null) return ApiResponse<object>.Fail(invalid);
            var token = await _userManager.GeneratePasswordResetTokenAsync(appUser);
            var result = await _userManager.ResetPasswordAsync(appUser, token, request.NewPassword);
            if (!result.Succeeded) return ApiResponse<object>.Fail(result.Errors.Select(e => e.Description).ToList());
            await _userManager.ResetAccessFailedCountAsync(appUser);
            await _userManager.SetLockoutEndDateAsync(appUser, null);
        }
        else if (!string.IsNullOrWhiteSpace(request.NewPin))
        {
            user.Passcode = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
            user.FailedLoginCount = 0;
            user.LockoutEndsAt = null;
        }
        else
        {
            return ApiResponse<object>.Fail("Provide newPassword or newPin.");
        }

        code.UsedAt = Now;
        await _db.SaveChangesAsync(ct);
        await RevokeAllAsync(user.Id, ct);
        return ApiResponse<object>.Ok(null, "Your credentials were reset. Please sign in.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ApiResponse<AuthTokenResponse> Invalid() => ApiResponse<AuthTokenResponse>.Fail("Invalid credentials.", 401);
    private static ApiResponse<AuthTokenResponse> Locked()
        => ApiResponse<AuthTokenResponse>.Fail($"Too many failed attempts. Try again in {LockoutDuration.TotalMinutes:N0} minutes or reset your password.", 423);

    private async Task<User?> FindForResetAsync(string? email, string? phone, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var appUser = await _userManager.FindByEmailAsync(email.Trim());
            return appUser == null ? null
                : await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.AuthId == appUser.Id && !u.IsDeleted, ct);
        }
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var trimmed = phone.Trim();
            return await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Phone == trimmed && !u.IsDeleted, ct);
        }
        return null;
    }

    /// <summary>First branch in the user's list that still exists in their tenant; falls back to the home branch.</summary>
    private async Task<Guid> DefaultShopAsync(User user, CancellationToken ct)
    {
        if (user.ShopIds.Count == 0) return user.BranchId;
        var existing = await _db.Shops.IgnoreQueryFilters()
            .Where(s => user.ShopIds.Contains(s.Id) && s.TenantId == user.TenantId && !s.IsDeleted)
            .Select(s => s.Id).ToListAsync(ct);
        return user.ShopIds.FirstOrDefault(existing.Contains) is var id && id != Guid.Empty ? id : user.BranchId;
    }

    private async Task<AuthTokenResponse> IssueTokensAsync(User user, Guid shopId, CancellationToken ct, RefreshToken? replacing = null)
    {
        var roles = await _db.Roles.Where(r => user.RoleIds.Contains(r.Id)).Select(r => r.Name!).ToListAsync(ct);

        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = Now.AddHours(double.Parse(_config["Jwt:ExpiryHours"] ?? "24", System.Globalization.CultureInfo.InvariantCulture));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new("tenantId", user.TenantId.ToString()),
            new("shopId", shopId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            notBefore: Now,
            expires: expiry,
            signingCredentials: creds);

        var refreshValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshDays = int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 30;
        var refresh = new RefreshToken
        {
            UserId      = user.Id,
            TenantId    = user.TenantId,
            ShopId      = shopId,
            TokenHash   = Hash(refreshValue),
            CreatedOn   = Now,
            ExpiresAt   = Now.AddDays(refreshDays),
            CreatedByIp = _currentUser.IpAddress
        };
        _db.RefreshTokens.Add(refresh);
        if (replacing != null)
        {
            replacing.RevokedAt = Now;
            replacing.ReplacedByTokenId = refresh.Id;
        }

        return new AuthTokenResponse(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(jwt),
            RefreshToken: refreshValue,
            ExpiresAt: expiry,
            UserName: user.Name,
            Roles: roles,
            UserId: user.Id,
            TenantId: user.TenantId,
            ShopId: shopId,
            RefreshTokenExpiresAt: refresh.ExpiresAt);
    }

    private Task RevokeAllAsync(Guid userId, CancellationToken ct)
        => _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
