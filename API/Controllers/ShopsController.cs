using Application.Interfaces.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Shop;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/shops")]
[Authorize]
public class ShopsController
    : BaseCrudController<Shop, CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel, ShopListViewModel, BaseSearchModel>
{
    private readonly IShopService<CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel> _shopService;

    public ShopsController(IShopService<CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel> service) : base(service)
    {
        _shopService = service;
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,Owner")]
    public override async Task<IActionResult> Create([FromBody] CreateShopViewModel vm, CancellationToken ct)
    {
        var result = await _shopService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
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
