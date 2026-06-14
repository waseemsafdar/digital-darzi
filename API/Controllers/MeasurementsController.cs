using Application.Interfaces.Services;
using Application.ViewModels.Measurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/measurements")]
[Authorize]
public class MeasurementsController : ControllerBase
{
    private readonly IMeasurementService _service;
    public MeasurementsController(IMeasurementService service) => _service = service;

    // ── Fields ──────────────────────────────────────────────────────────────
    [HttpGet("fields")]
    public async Task<IActionResult> GetFields(CancellationToken ct)
        => Ok(await _service.GetFieldsAsync(ct));

    [HttpPost("fields")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> CreateField([FromBody] CreateMeasurementFieldViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateFieldAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("fields/{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateField(Guid id, [FromBody] UpdateMeasurementFieldViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateFieldAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("fields/{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> DeleteField(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteFieldAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Templates ────────────────────────────────────────────────────────────
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
        => Ok(await _service.GetTemplatesAsync(ct));

    [HttpGet("templates/{id:guid}")]
    public async Task<IActionResult> GetTemplateDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetTemplateDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("templates")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateTemplateAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateTemplateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("templates/{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteTemplateAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Profiles ─────────────────────────────────────────────────────────────
    [HttpGet("profiles/customer/{customerId:guid}")]
    public async Task<IActionResult> GetCustomerProfiles(Guid customerId, CancellationToken ct)
        => Ok(await _service.GetCustomerProfilesAsync(customerId, ct));

    [HttpGet("profiles/{id:guid}")]
    public async Task<IActionResult> GetProfileDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetProfileDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("profiles")]
    public async Task<IActionResult> SaveProfile([FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
    {
        var result = await _service.SaveProfileAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("profiles/{id:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateProfileAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("profiles/{id:guid}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteProfileAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
