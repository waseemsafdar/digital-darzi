using Application.Interfaces.Services;
using Application.ViewModels.Measurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/measurements")]
[Authorize]
public class MeasurementsController : BaseController
{
    private readonly IMeasurementService _service;
    public MeasurementsController(IMeasurementService service) => _service = service;

    // ── Fields ──────────────────────────────────────────────────────────────
    [HttpGet("fields")]
    public async Task<IActionResult> GetFields(CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetFieldsAsync(ct));

    [HttpPost("fields")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> CreateField([FromBody] CreateMeasurementFieldViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateFieldAsync(vm, ct));

    [HttpPut("fields/{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateField(Guid id, [FromBody] UpdateMeasurementFieldViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateFieldAsync(id, vm, ct));

    [HttpDelete("fields/{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> DeleteField(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeleteFieldAsync(id, ct));

    // ── Templates ────────────────────────────────────────────────────────────
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetTemplatesAsync(ct));

    [HttpGet("templates/{id}")]
    public async Task<IActionResult> GetTemplateDetail(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetTemplateDetailAsync(id, ct));

    [HttpPost("templates")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateTemplateAsync(vm, ct));

    [HttpPut("templates/{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateTemplateAsync(id, vm, ct));

    [HttpDelete("templates/{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeleteTemplateAsync(id, ct));

    // ── Profiles ─────────────────────────────────────────────────────────────
    [HttpGet("profiles/customer/{customerId}")]
    public async Task<IActionResult> GetCustomerProfiles(Guid customerId, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetCustomerProfilesAsync(customerId, ct));

    [HttpGet("profiles/{id}")]
    public async Task<IActionResult> GetProfileDetail(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetProfileDetailAsync(id, ct));

    [HttpPost("profiles")]
    public async Task<IActionResult> SaveProfile([FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveProfileAsync(vm, ct));

    [HttpPut("profiles/{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateProfileAsync(id, vm, ct));

    [HttpDelete("profiles/{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.DeleteProfileAsync(id, ct));
}
