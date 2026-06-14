using Application.Common;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Generic base controller — all CRUD controllers should inherit from this.
/// Returns StatusCode(resp.Success ? 200 : errorCode, resp) — no raw Ok()/NotFound().
/// </summary>
[ApiController]
public abstract class BaseCrudController<TCreate, TUpdate, TDetail> : ControllerBase
    where TCreate : class
    where TUpdate : class
    where TDetail : class
{
    protected readonly IBaseCrudService<TCreate, TUpdate, TDetail> _service;

    protected BaseCrudController(IBaseCrudService<TCreate, TUpdate, TDetail> service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public virtual async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet]
    public virtual async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost]
    public virtual async Task<IActionResult> Create([FromBody] TCreate vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public virtual async Task<IActionResult> Update(Guid id, [FromBody] TUpdate vm, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
