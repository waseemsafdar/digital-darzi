using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext db,
        IConfiguration config)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _config = config;
    }

    // ── Register (removed) ─────────────────────────────────────────────────
    // Owner registration is now handled exclusively by SystemAdmin via:
    //   POST /api/admin/provision-owner   (Step 1: create owner user)
    //   POST /api/admin/provision-shop    (Step 2: create shop + link)
    //   POST /api/admin/provision-subscription (Step 3: set subscription)

    // ── Login ──────────────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> LoginAsync(AuthLoginRequest req, CancellationToken ct = default)
    {
        User? domainUser = null;

        if (!string.IsNullOrWhiteSpace(req.Email) && !string.IsNullOrWhiteSpace(req.Password))
        {
            var appUser = await _userManager.FindByEmailAsync(req.Email);
            if (appUser == null) return ApiResponse<AuthTokenResponse>.Fail("Invalid credentials.");
            if (!await _userManager.CheckPasswordAsync(appUser, req.Password))
                return ApiResponse<AuthTokenResponse>.Fail("Invalid credentials.");

            domainUser = await _db.AppUsers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.AuthId == appUser.Id && !u.IsDeleted, ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.Passcode))
        {
            domainUser = await _db.AppUsers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Phone == req.Phone && !u.IsDeleted, ct);
            if (domainUser?.Passcode == null || !BCrypt.Net.BCrypt.Verify(req.Passcode, domainUser.Passcode))
                return ApiResponse<AuthTokenResponse>.Fail("Invalid credentials.");
        }
        else
        {
            return ApiResponse<AuthTokenResponse>.Fail("Provide email+password or phone+PIN.");
        }

        if (domainUser == null || domainUser.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<AuthTokenResponse>.Fail("Account is inactive.");

        domainUser.LastLoginDate = DateTime.UtcNow;
        domainUser.LoginCount++;
        await _db.SaveChangesAsync(ct);

        var roles = await GetRoleNamesAsync(domainUser.RoleIds);
        var shopId = domainUser.ShopIds.FirstOrDefault();
        return ApiResponse<AuthTokenResponse>.Ok(GenerateToken(domainUser, shopId, roles));
    }

    public Task<ApiResponse<AuthTokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult(ApiResponse<AuthTokenResponse>.Fail("Refresh token not yet implemented."));

    // ── Switch Branch ──────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> SwitchBranchAsync(Guid userId, Guid shopId, CancellationToken ct = default)
    {
        // Load domain user (ignore tenant filter — we only have userId, not tenant scoped context here)
        var domainUser = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

        if (domainUser == null || domainUser.ActiveStatus == ActiveStatus.Inactive)
            return ApiResponse<AuthTokenResponse>.Fail("User not found or inactive.");

        // Guard: user must be assigned to the requested shop
        if (!domainUser.ShopIds.Contains(shopId))
            return ApiResponse<AuthTokenResponse>.Fail("You do not have access to this branch.");

        // Verify the shop exists and is active (use IgnoreQueryFilters since we have no branch context yet)
        var shop = await _db.Shops.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == shopId && !s.IsDeleted, ct);

        if (shop == null)
            return ApiResponse<AuthTokenResponse>.Fail("Branch not found.");

        var roles = await GetRoleNamesAsync(domainUser.RoleIds);
        return ApiResponse<AuthTokenResponse>.Ok(GenerateToken(domainUser, shopId, roles), $"Switched to branch '{shop.Name}'.");
    }

    public async Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, string current, string newPwd, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (domainUser?.AuthId == null) return ApiResponse<object>.Fail("User not found.");
        var appUser = await _userManager.FindByIdAsync(domainUser.AuthId.ToString()!);
        if (appUser == null) return ApiResponse<object>.Fail("Auth user not found.");
        var result = await _userManager.ChangePasswordAsync(appUser, current, newPwd);
        return result.Succeeded ? ApiResponse<object>.Ok((object?)null, "Password changed.")
                                : ApiResponse<object>.Fail(result.Errors.First().Description);
    }

    public async Task<ApiResponse<object>> ChangePinAsync(Guid userId, string current, string newPin, CancellationToken ct = default)
    {
        var domainUser = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (domainUser?.Passcode == null) return ApiResponse<object>.Fail("User not found or not a PIN user.");
        if (!BCrypt.Net.BCrypt.Verify(current, domainUser.Passcode))
            return ApiResponse<object>.Fail("Current PIN is incorrect.");
        domainUser.Passcode = BCrypt.Net.BCrypt.HashPassword(newPin);
        domainUser.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok((object?)null, "PIN changed.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private AuthTokenResponse GenerateToken(User user, Guid shopId, IEnumerable<string> roles)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiryHours"] ?? "24"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new("tenantId", user.TenantId.ToString()),
            new("shopId", shopId.ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new AuthTokenResponse(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt: expiry,
            UserName: user.Name,
            Roles: roles,
            UserId: user.Id,
            TenantId: user.TenantId,
            ShopId: shopId);
    }

    private async Task<IEnumerable<string>> GetRoleNamesAsync(List<Guid> roleIds)
    {
        var names = new List<string>();
        foreach (var id in roleIds)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());
            if (role?.Name != null) names.Add(role.Name);
        }
        return names;
    }
}
