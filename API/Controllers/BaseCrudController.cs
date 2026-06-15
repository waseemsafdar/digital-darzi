using Application.Common;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Generic base controller — all CRUD controllers should inherit from this.
/// Update: Id comes from the ViewModel body (IHasId), not from the route.
/// Mirrors MobilePosApi.BaseCrudController pattern.
/// </summary>
[ApiController]
public abstract class BaseCrudController<TCreate, TUpdate, TDetail> : BaseController
    where TCreate : class
    where TUpdate : class, IHasId
    where TDetail : class
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
    public virtual async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetAllAsync(page, pageSize, ct));

    [HttpPost]
    public virtual async Task<IActionResult> Create([FromBody] TCreate vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateAsync(vm, ct));

    /// <summary>Id must be included in the request body (vm.Id). No route parameter needed.</summary>
    [HttpPut]
    public virtual async Task<IActionResult> Update([FromBody] TUpdate vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(vm, ct));

    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeleteAsync(id, ct));
}
