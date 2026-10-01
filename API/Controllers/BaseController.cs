using Application.Common;
using Application.Interfaces.Services;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Base controller for all API controllers.
/// <c>ReturnProcessedResponse</c> converts an <see cref="ApiResponse{T}"/> into the HTTP result.
/// Failures use <see cref="ApiResponse{T}.StatusCode"/> when the service set one; otherwise the
/// historical inference from the error text applies ("not found" → 404, else 400).
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected IActionResult ReturnProcessedResponse<T>(ApiResponse<T> response)
    {
        if (response.Success)
            return Ok(response);

        var status = response.StatusCode ?? InferStatus(response.Errors.FirstOrDefault() ?? response.Message);
        return StatusCode(status, response);
    }

    protected IActionResult ReturnProcessedResponse(ApiResponse response)
        => response.Success ? Ok(response) : StatusCode(response.StatusCode ?? 400, response);

    /// <summary>201 Created for a successful create, otherwise the normal failure mapping.</summary>
    protected IActionResult ReturnCreated<T>(ApiResponse<T> response, string actionName, object routeValues)
        => response.Success ? CreatedAtAction(actionName, routeValues, response) : ReturnProcessedResponse(response);

    /// <summary>Runtime permission check for actions whose requirement depends on the request (e.g. report scope).</summary>
    protected async Task<bool> HasPermissionAsync(string permission)
    {
        var permissions = HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var user = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        return await permissions.HasPermissionAsync(user.TenantId, user.Roles, permission, HttpContext.RequestAborted);
    }

    protected IActionResult Forbidden(string message)
        => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Forbidden(message));

    private static int InferStatus(string? error)
    {
        if (error == null) return 400;
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase)) return 404;
        if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)) return 401;
        return 400;
    }
}
