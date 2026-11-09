using API.Authorization;
using Application.Common;
using Application.ViewModels.Operations;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// The shop's garment types. Built-in types can be renamed, hidden and reordered;
/// the owner can add their own. Reading is open to every signed-in user of the shop,
/// because every screen that shows an order or a job needs the names.
/// </summary>
[ApiController]
[Route("api/garment-types")]
[Authorize]
public class GarmentTypesController : BaseController
{
    private readonly IGarmentTypeService _service;
    public GarmentTypesController(IGarmentTypeService service) => _service = service;

    /// <summary>Visible types in display order. Pass includeHidden=true for the manage view (adds usage counts).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool includeHidden, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetAsync(includeHidden, ct));

    [HttpPost]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Create([FromBody] CreateGarmentTypeViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateAsync(vm, ct));

    /// <summary>Rename or hide/show a type (built-in or custom).</summary>
    [HttpPut("{code:int}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Update(int code, [FromBody] UpdateGarmentTypeViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(code, vm, ct));

    [HttpPut("order")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Reorder([FromBody] ReorderGarmentTypesViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.ReorderAsync(vm, ct));

    /// <summary>Deletes a custom type nothing uses yet. Built-in or used types must be hidden instead.</summary>
    [HttpDelete("{code:int}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Delete(int code, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteAsync(code, ct));
}
