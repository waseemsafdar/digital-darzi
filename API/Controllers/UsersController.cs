using Application.Interfaces.Services;
using Application.ViewModels.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "SystemAdmin,Owner,Manager")]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;
    public UsersController(IUserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetListAsync(shopId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Create([FromBody] CreateUserViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _service.DeactivateAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}/shops")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> AssignShops(Guid id, [FromBody] List<Guid> shopIds, CancellationToken ct)
    {
        var result = await _service.AssignShopsAsync(id, shopIds, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}/roles")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] List<Guid> roleIds, CancellationToken ct)
    {
        var result = await _service.AssignRolesAsync(id, roleIds, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
