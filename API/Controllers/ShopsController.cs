using Application.Interfaces.Services;
using Application.ViewModels.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/shops")]
[Authorize]
public class ShopsController
    : BaseCrudController<CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel>
{
    private readonly IShopService _shopService;

    public ShopsController(IShopService service) : base(service)
    {
        _shopService = service;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _shopService.GetListAsync(page, pageSize, ct));

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public override async Task<IActionResult> Create([FromBody] CreateShopViewModel vm, CancellationToken ct)
    {
        var result = await _shopService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [Authorize(Roles = "SystemAdmin,Owner,Manager")]
    public override async Task<IActionResult> Update([FromBody] UpdateShopViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _shopService.UpdateAsync(vm, ct));

    [HttpDelete("{id}")]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public override async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _shopService.DeleteAsync(id, ct));
}
