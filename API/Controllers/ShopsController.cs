using API.Authorization;
using Application.Common;
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
        => _shopService = service;

    /// <summary>Branches of the caller's business.</summary>
    [HttpGet]
    public override Task<IActionResult> GetAll([FromQuery] BaseSearchModel search, CancellationToken ct) => base.GetAll(search, ct);

    [HttpGet("{id}")]
    public override Task<IActionResult> GetById(Guid id, CancellationToken ct) => base.GetById(id, ct);

    /// <summary>Adds a branch: the Owner gets access to it and it receives its own measurement catalogue.</summary>
    [HttpPost]
    [HasPermission(Permissions.ShopsManage)]
    public override async Task<IActionResult> Create([FromBody] CreateShopViewModel vm, CancellationToken ct)
    {
        var result = await _shopService.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result) : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [HasPermission(Permissions.ShopsEdit)]
    public override Task<IActionResult> Update([FromBody] UpdateShopViewModel vm, CancellationToken ct) => base.Update(vm, ct);

    [HttpDelete("{id}")]
    [HasPermission(Permissions.ShopsManage)]
    public override Task<IActionResult> Delete(Guid id, CancellationToken ct) => base.Delete(id, ct);

    [HttpPut("{id}/subscription")]
    [Authorize(Roles = AppRoles.SystemAdmin)]
    public async Task<IActionResult> UpdateSubscription(Guid id, [FromBody] UpdateShopSubscriptionRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _shopService.UpdateSubscriptionAsync(id, request, ct));
}
