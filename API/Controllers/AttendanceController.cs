using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/attendance")]
[Authorize]
public class AttendanceController
    : BaseCrudController<StaffAttendance, RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel, AttendanceListViewModel, AttendanceSearchModel>
{
    private readonly IStaffAttendanceService<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel> _attendanceService;

    public AttendanceController(IStaffAttendanceService<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel> service) : base(service)
    {
        _attendanceService = service;
    }

    /// <summary>Returns the current staff member's own attendance history (defaults to current month).</summary>
    [HttpGet("my-attendance")]
    public async Task<IActionResult> GetMyAttendance(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct)
        => ReturnProcessedResponse(await _attendanceService.GetMyAttendanceAsync(from, to, month, year, ct));

    /// <summary>Self-service check-in: marks the caller as Present for today with current time.
    /// If already checked in today, updates check-out time instead.</summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] StaffCheckInViewModel vm, CancellationToken ct)
    {
        var result = await _attendanceService.CheckInAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : ReturnProcessedResponse(result);
    }

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
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : ReturnProcessedResponse(result);
    }
}
