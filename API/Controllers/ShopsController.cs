using Application.Interfaces.Services;
using Application.ViewModels.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/shops")]
[Authorize]
public class ShopsController : ControllerBase
{
    private readonly IShopService _service;
    public ShopsController(IShopService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.GetListAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Create([FromBody] CreateShopViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,Owner,Manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShopViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
