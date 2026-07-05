using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : BaseController
{
    private readonly IUserService _service;
    public RolesController(IUserService service) => _service = service;

    /// <summary>
    /// Returns all assignable roles (SystemAdmin excluded).
    /// Used by the frontend when building role-assignment dropdowns for users.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetRolesAsync(ct));
}
