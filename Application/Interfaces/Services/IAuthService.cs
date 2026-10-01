using Application.Common;

namespace Application.Interfaces.Services;

public record AuthLoginRequest(
    string? Email,
    string? Phone,
    string? Password,
    string? Passcode
);

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserName,
    IEnumerable<string> Roles,
    Guid UserId,
    Guid TenantId,
    Guid ShopId,
    DateTime RefreshTokenExpiresAt = default
);

public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken, bool AllDevices = false);
public record ForgotPasswordRequest(string? Email, string? Phone);
public record ResetPasswordRequest(string? Email, string? Phone, string Code, string? NewPassword, string? NewPin);

/// <summary>A branch the signed-in user may switch to.</summary>
public record BranchSummary(Guid Id, string Name, string? City);

/// <summary>Subscription state of the tenant, so a client can warn before access is cut off.</summary>
public record SubscriptionSummary(Domain.Enums.SubscriptionStatus? Status, DateTime? TrialEndsAt, DateTime? SubscriptionEndsAt, string? PlanName);

/// <summary>
/// Everything a client needs to render the signed-in session: identity, the active branch,
/// the branches the user may switch to, and the permissions this session actually holds
/// (role defaults plus this tenant's overrides — the same rules the API enforces).
/// </summary>
public record MeResponse(
    Guid UserId,
    string Name,
    string? Email,
    string? Phone,
    Guid TenantId,
    Guid ShopId,
    string? ShopName,
    string Currency,
    string CurrencySymbol,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions,
    IEnumerable<BranchSummary> Branches,
    SubscriptionSummary? Subscription);

public interface IAuthService
{
    Task<ApiResponse<AuthTokenResponse>> LoginAsync(AuthLoginRequest request, CancellationToken ct = default);
    Task<ApiResponse<AuthTokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<ApiResponse<object>> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct = default);
    Task<ApiResponse<AuthTokenResponse>> SwitchBranchAsync(Guid userId, Guid shopId, CancellationToken ct = default);
    Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<ApiResponse<object>> ChangePinAsync(Guid userId, string currentPin, string newPin, CancellationToken ct = default);
    Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>The current session: identity, active branch, switchable branches and effective permissions.</summary>
    Task<ApiResponse<MeResponse>> GetMeAsync(CancellationToken ct = default);
}
