using Application.Common;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace API.Controllers;

/// <summary>
/// Base controller for all API controllers.
/// Provides <c>ReturnProcessedResponse</c> to convert an <see cref="ApiResponse{T}"/>
/// into the correct HTTP result — mirrors MobilePosApi.BaseController pattern.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Translates an <see cref="ApiResponse{T}"/> into the matching HTTP action result.
    /// Uses the <c>Success</c> flag + optional HTTP status code on the response.
    /// </summary>
    protected IActionResult ReturnProcessedResponse<T>(ApiResponse<T> response)
    {
        if (response.Success)
            return Ok(response);

        // Use error hint from Errors list to infer status, defaulting to 400
        var firstError = response.Errors.FirstOrDefault() ?? string.Empty;

        if (firstError.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(response);

        if (firstError.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(response);

        return BadRequest(response);
    }

    /// <summary>
    /// Overload for non-generic <see cref="ApiResponse"/> (delete / void responses).
    /// </summary>
    protected IActionResult ReturnProcessedResponse(ApiResponse response)
        => response.Success ? Ok(response) : BadRequest(response);
}
