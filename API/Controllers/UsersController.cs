using Application.Interfaces.Services;
using Application.ViewModels.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "SystemAdmin,Owner,Manager")]
public class UsersController : BaseController
{
    private readonly IUserService _service;
    public UsersController(IUserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetListAsync(shopId, page, pageSize, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetDetailAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Create([FromBody] CreateUserViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(vm, ct));

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeactivateAsync(id, ct));

    [HttpPut("{id}/shops")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> AssignShops(Guid id, [FromBody] List<Guid> shopIds, CancellationToken ct)
        => ReturnProcessedResponse(await _service.AssignShopsAsync(id, shopIds, ct));

    [HttpPut("{id}/roles")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] List<Guid> roleIds, CancellationToken ct)
        => ReturnProcessedResponse(await _service.AssignRolesAsync(id, roleIds, ct));
}
