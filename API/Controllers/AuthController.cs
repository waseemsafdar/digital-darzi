using Application.Common;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Register a new shop + owner account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AuthRegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Login via email+password or phone+PIN.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AuthLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Refresh access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(refreshToken, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Change email/password login password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var result = await _authService.ChangePasswordAsync(currentUser.UserId, request.CurrentPassword, request.NewPassword, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Change phone+PIN login passcode.</summary>
    [HttpPost("change-pin")]
    [Authorize]
    public async Task<IActionResult> ChangePin(
        [FromBody] ChangePinRequest request,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var result = await _authService.ChangePinAsync(currentUser.UserId, request.CurrentPin, request.NewPin, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ChangePinRequest(string CurrentPin, string NewPin);
