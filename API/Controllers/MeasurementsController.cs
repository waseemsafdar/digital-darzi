using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Measurement;
using Application.ViewModels.Operations;
using Domain.Enums;
using Infrastructure.Services;
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
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetFields(CancellationToken ct) => ReturnProcessedResponse(await _service.GetFieldsAsync(ct));

    [HttpPost("fields")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> CreateField([FromBody] CreateMeasurementFieldViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateFieldAsync(vm, ct));

    [HttpPut("fields/{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> UpdateField(Guid id, [FromBody] UpdateMeasurementFieldViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateFieldAsync(id, vm, ct));

    [HttpDelete("fields/{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> DeleteField(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteFieldAsync(id, ct));

    // ── Templates ────────────────────────────────────────────────────────────
    [HttpGet("templates")]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetTemplates(CancellationToken ct) => ReturnProcessedResponse(await _service.GetTemplatesAsync(ct));

    [HttpGet("templates/{id}")]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetTemplateDetail(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.GetTemplateDetailAsync(id, ct));

    [HttpPost("templates")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateTemplateAsync(vm, ct));

    [HttpPut("templates/{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateTemplateAsync(id, vm, ct));

    [HttpDelete("templates/{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteTemplateAsync(id, ct));

    // ── Profiles ─────────────────────────────────────────────────────────────
    [HttpGet("profiles/customer/{customerId}")]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetCustomerProfiles(Guid customerId, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetCustomerProfilesAsync(customerId, ct));

    [HttpGet("profiles/{id}")]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetProfileDetail(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.GetProfileDetailAsync(id, ct));

    /// <summary>Every saved version of a profile, newest first.</summary>
    [HttpGet("profiles/{id}/versions")]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> GetProfileVersions(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.GetProfileVersionsAsync(id, ct));

    [HttpPost("profiles")]
    [HasPermission(Permissions.MeasurementsEdit)]
    public async Task<IActionResult> SaveProfile([FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveProfileAsync(vm, ct));

    /// <summary>Saves new values as a new version. Orders keep the measurements they were created with.</summary>
    [HttpPut("profiles/{id}")]
    [HasPermission(Permissions.MeasurementsEdit)]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] SaveMeasurementProfileViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateProfileAsync(id, vm, ct));

    [HttpDelete("profiles/{id}")]
    [HasPermission(Permissions.MeasurementsEdit)]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteProfileAsync(id, ct));
}

/// <summary>Configurable style options per garment type (collar, cuff, pockets, buttons, ...).</summary>
[ApiController]
[Route("api/garment-attributes")]
[Authorize]
public class GarmentAttributesController : BaseController
{
    private readonly IGarmentAttributeService _service;
    public GarmentAttributesController(IGarmentAttributeService service) => _service = service;

    [HttpGet]
    [HasPermission(Permissions.MeasurementsView)]
    public async Task<IActionResult> Get([FromQuery] GarmentType? garmentType, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetAsync(garmentType, ct));

    [HttpPost]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Create([FromBody] SaveGarmentAttributeViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateAsync(vm, ct));

    [HttpPut("{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveGarmentAttributeViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(id, vm, ct));

    [HttpDelete("{id}")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteAsync(id, ct));

    /// <summary>Adds the starter attribute catalogue and expense categories to a branch that has none.</summary>
    [HttpPost("seed-defaults")]
    [HasPermission(Permissions.MeasurementsConfigure)]
    public async Task<IActionResult> SeedDefaults(CancellationToken ct) => ReturnProcessedResponse(await _service.SeedDefaultsAsync(ct));
}
