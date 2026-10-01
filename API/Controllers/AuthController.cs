using System.Text.Json;
using Application.Common;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Login via email+password or phone+PIN. Returns an access token and a rotating refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AuthLoginRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.LoginAsync(request, ct));

    /// <summary>
    /// Exchange a refresh token for a new access + refresh token pair. Body: <c>{ "refreshToken": "..." }</c>
    /// (a bare JSON string is also accepted for older clients). Each refresh token works once.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] JsonElement body, CancellationToken ct)
    {
        var token = body.ValueKind switch
        {
            JsonValueKind.String => body.GetString(),
            JsonValueKind.Object when body.TryGetProperty("refreshToken", out var p) && p.ValueKind == JsonValueKind.String => p.GetString(),
            _ => null
        };
        return ReturnProcessedResponse(await _authService.RefreshAsync(token ?? string.Empty, ct));
    }

    /// <summary>Revoke a refresh token, or every session with <c>allDevices: true</c>.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, [FromServices] ICurrentUserService currentUser, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.LogoutAsync(currentUser.UserId, request, ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, [FromServices] ICurrentUserService currentUser, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.ChangePasswordAsync(currentUser.UserId, request.CurrentPassword, request.NewPassword, ct));

    [HttpPost("change-pin")]
    [Authorize]
    public async Task<IActionResult> ChangePin([FromBody] ChangePinRequest request, [FromServices] ICurrentUserService currentUser, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.ChangePinAsync(currentUser.UserId, request.CurrentPin, request.NewPin, ct));

    /// <summary>Switch the caller's active branch. Returns new tokens scoped to the target shop.</summary>
    [HttpPost("switch-branch")]
    [Authorize]
    public async Task<IActionResult> SwitchBranch([FromBody] SwitchBranchRequest request, [FromServices] ICurrentUserService currentUser, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.SwitchBranchAsync(currentUser.UserId, request.ShopId, ct));

    /// <summary>Send a 6-digit reset code by SMS (phone) or email. The response never reveals whether the account exists.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.ForgotPasswordAsync(request, ct));

    /// <summary>Reset the password (email users) or PIN (phone users) with the code.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _authService.ResetPasswordAsync(request, ct));

    /// <summary>
    /// The signed-in session: identity, active branch, branches the user can switch to, effective
    /// permissions (role defaults plus tenant overrides) and subscription state. Clients use this to
    /// decide which screens and buttons to show; the API still enforces every permission itself.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [DisableRateLimiting]   // read on every page load; the auth limiter is for credential endpoints
    [ProducesResponseType(typeof(ApiResponse<MeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
        => ReturnProcessedResponse(await _authService.GetMeAsync(ct));
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ChangePinRequest(string CurrentPin, string NewPin);
public record SwitchBranchRequest(Guid ShopId);
