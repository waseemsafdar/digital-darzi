using Application.Common;

namespace Application.Interfaces.Services;

public record AuthRegisterRequest(
    string ShopName,
    string OwnerName,
    string? Email,
    string? Phone,
    string? Password,
    string? Passcode,
    string? City
);

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
    Guid ShopId
);

public interface IAuthService
{
    Task<ApiResponse<AuthTokenResponse>> RegisterAsync(AuthRegisterRequest request, CancellationToken ct = default);
    Task<ApiResponse<AuthTokenResponse>> LoginAsync(AuthLoginRequest request, CancellationToken ct = default);
    Task<ApiResponse<AuthTokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<ApiResponse<object>> ChangePinAsync(Guid userId, string currentPin, string newPin, CancellationToken ct = default);
}
