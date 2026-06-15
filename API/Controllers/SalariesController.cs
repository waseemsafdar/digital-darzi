using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/salaries")]
[Authorize(Roles = "Owner,Manager")]
public class SalariesController
    : BaseCrudController<RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel>
{
    private readonly IStaffSalaryService _salaryService;

    public SalariesController(IStaffSalaryService service) : base(service)
    {
        _salaryService = service;
    }

    /// <summary>Get paginated salaries with optional month/year/staff filters.</summary>
    [HttpGet]
    public override async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _salaryService.GetFilteredAsync(null, null, null, page, pageSize, ct));

    [HttpGet("filter")]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] Guid? staffId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _salaryService.GetFilteredAsync(month, year, staffId, page, pageSize, ct));

    [HttpPost]
    public override async Task<IActionResult> Create([FromBody] RecordStaffSalaryViewModel vm, CancellationToken ct)
    {
        var result = await _salaryService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }
}
