using API.Authorization;
using Application.Common;
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
    private readonly IStaffAttendanceService<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel> _attendance;

    public AttendanceController(IStaffAttendanceService<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel> service) : base(service)
        => _attendance = service;

    /// <summary>The caller's own attendance (defaults to the current month).</summary>
    [HttpGet("my-attendance")]
    public async Task<IActionResult> GetMyAttendance([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? month, [FromQuery] int? year, CancellationToken ct)
        => ReturnProcessedResponse(await _attendance.GetMyAttendanceAsync(from, to, month, year, ct));

    /// <summary>Self-service punch: first call today checks in (shop-local time), the next records check-out.</summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] StaffCheckInViewModel vm, CancellationToken ct)
    {
        var result = await _attendance.CheckInAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result) : ReturnProcessedResponse(result);
    }

    [HttpGet("summary")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetSummary([FromQuery] Guid staffId, [FromQuery] int month, [FromQuery] int year, CancellationToken ct)
        => ReturnProcessedResponse(await _attendance.GetSummaryAsync(staffId, month, year, ct));

    [HttpGet("{id}")]
    [HasPermission(Permissions.FinanceView)]
    public override Task<IActionResult> GetById(Guid id, CancellationToken ct) => base.GetById(id, ct);

    [HttpGet]
    [HasPermission(Permissions.FinanceView)]
    public override Task<IActionResult> GetAll([FromQuery] AttendanceSearchModel search, CancellationToken ct) => base.GetAll(search, ct);

    [HttpPost]
    [HasPermission(Permissions.FinanceManage)]
    public override async Task<IActionResult> Create([FromBody] RecordAttendanceViewModel vm, CancellationToken ct)
    {
        var result = await _attendance.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result) : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [HasPermission(Permissions.FinanceManage)]
    public override Task<IActionResult> Update([FromBody] UpdateAttendanceViewModel vm, CancellationToken ct) => base.Update(vm, ct);

    [HttpDelete("{id}")]
    [HasPermission(Permissions.FinanceManage)]
    public override Task<IActionResult> Delete(Guid id, CancellationToken ct) => base.Delete(id, ct);
}
