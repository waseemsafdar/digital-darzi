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
    private readonly ITenantSetupService _tenantSetup;
    private readonly IConfiguration _config;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext db,
        ITenantSetupService tenantSetup,
        IConfiguration config)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _tenantSetup = tenantSetup;
        _config = config;
    }

    // ── Register ───────────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthTokenResponse>> RegisterAsync(AuthRegisterRequest req, CancellationToken ct = default)
    {
        var tenantId = Guid.NewGuid();
        var shopId   = Guid.NewGuid();
        var now      = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var shop = new Shop
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = shopId,
                Name = req.ShopName,
                City = req.City,
                ActiveStatus = ActiveStatus.Active,
                CreatedOn = now,
                UpdatedOn = now
            };
            _db.Shops.Add(shop);

            var domainUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = shopId,
                Name = req.OwnerName,
                Email = req.Email,
                Phone = req.Phone,
                ShopIds = new List<Guid> { shopId },
                ActiveStatus = ActiveStatus.Active,
                CreatedOn = now,
                UpdatedOn = now
            };

            var ownerRole = await _roleManager.FindByNameAsync("Owner");
            if (ownerRole != null) domainUser.RoleIds = new List<Guid> { ownerRole.Id };

            if (!string.IsNullOrWhiteSpace(req.Email) && !string.IsNullOrWhiteSpace(req.Password))
            {
                var appUser = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = req.Email,
                    Email = req.Email,
                    EmailConfirmed = true
                };
                var result = await _userManager.CreateAsync(appUser, req.Password);
                if (!result.Succeeded)
                    return ApiResponse<AuthTokenResponse>.Fail(result.Errors.Select(e => e.Description).ToList());
                await _userManager.AddToRoleAsync(appUser, "Owner");
                domainUser.AuthId = appUser.Id;
            }
            else if (!string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.Passcode))
            {
                domainUser.Passcode = BCrypt.Net.BCrypt.HashPassword(req.Passcode);
            }
            else
            {
                return ApiResponse<AuthTokenResponse>.Fail("Provide either email+password or phone+PIN.");
            }

            _db.AppUsers.Add(domainUser);
            shop.OwnerId = domainUser.Id;
            shop.CreatedBy = domainUser.Id;
            shop.UpdatedBy = domainUser.Id;

            await _db.SaveChangesAsync(ct);
            await _tenantSetup.SetupNewTenantAsync(tenantId, shopId, domainUser.Id, ct);
            await tx.CommitAsync(ct);

            var roleNames = await GetRoleNamesAsync(domainUser.RoleIds);
            var token = GenerateToken(domainUser, shopId, roleNames);
            return ApiResponse<AuthTokenResponse>.Ok(token);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

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
