using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Common;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Generic base controller — all CRUD controllers should inherit from this.
/// Matches MobilePosApi.BaseCrudController pattern.
/// </summary>
[ApiController]
public abstract class BaseCrudController<TEntity, TCreate, TUpdate, TDetail, TList, TSearch> : BaseController
    where TEntity : BaseDBModel
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
    where TList   : class, new()
    where TSearch : class, IBaseSearchModel, new()
{
    protected readonly IBaseCrudService<TCreate, TUpdate, TDetail> _service;

    protected BaseCrudController(IBaseCrudService<TCreate, TUpdate, TDetail> service)
    {
        _service = service;
    }

    [HttpGet("{id}")]
    public virtual async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetByIdAsync(id, ct));

    [HttpGet]
    public virtual async Task<IActionResult> GetAll([FromQuery] TSearch search, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetAllAsync(search, ct));

    [HttpPost]
    public virtual async Task<IActionResult> Create([FromBody] TCreate vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateAsync(vm, ct));

    [HttpPut]
    public virtual async Task<IActionResult> Update([FromBody] TUpdate vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(vm, ct));

    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeleteAsync(id, ct));
}
