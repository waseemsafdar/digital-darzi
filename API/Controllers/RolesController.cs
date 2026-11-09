using API.Authorization;
using Application.Common;
using Application.ViewModels.User;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// The business's roles. Everyone signed in can read them (the app needs the names and which
/// screens a role opens); only the Owner can add, change or delete them.
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : BaseController
{
    private readonly IRoleService _service;
    public RolesController(IRoleService service) => _service = service;

    /// <summary>Built-in and the business's own roles, in display order. includeHidden=true for the manage view.</summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles([FromQuery] bool includeHidden, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetAsync(includeHidden, ct));

    /// <summary>Every permission key, and whether it can be given to a role other than Owner.</summary>
    [HttpGet("permissions")]
    public IActionResult GetPermissions() => ReturnProcessedResponse(_service.GetPermissionCatalogue());

    [HttpPost]
    [HasPermission(Permissions.SettingsPermissions)]
    public async Task<IActionResult> Create([FromBody] SaveRoleViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateAsync(vm, ct));

    /// <summary>Rename, describe, hide/show and set the full permission list. Owner cannot be changed.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.SettingsPermissions)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRoleViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(id, vm, ct));

    /// <summary>Deletes one of the business's own roles that no active staff member holds.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.SettingsPermissions)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteAsync(id, ct));
}
