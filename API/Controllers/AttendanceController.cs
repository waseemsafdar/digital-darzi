using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/attendance")]
[Authorize]
public class AttendanceController
    : BaseCrudController<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel>
{
    private readonly IStaffAttendanceService _attendanceService;

    public AttendanceController(IStaffAttendanceService service) : base(service)
    {
        _attendanceService = service;
    }

    /// <summary>Get paginated attendance with optional staff/date filters.</summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Manager")]
    public override async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _attendanceService.GetFilteredAsync(null, null, null, page, pageSize, ct));

    [HttpGet("filter")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] Guid? staffId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _attendanceService.GetFilteredAsync(staffId, from, to, page, pageSize, ct));

    [HttpGet("summary")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid staffId,
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken ct)
        => ReturnProcessedResponse(await _attendanceService.GetSummaryAsync(staffId, month, year, ct));

    [HttpPost]
    public override async Task<IActionResult> Create([FromBody] RecordAttendanceViewModel vm, CancellationToken ct)
    {
        var result = await _attendanceService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }
}
