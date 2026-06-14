using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Roles = "Owner,Manager")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _service;
    public FinanceController(IFinanceService service) => _service = service;

    // ── Dashboard ─────────────────────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetSummaryAsync(dateFrom, dateTo, ct));
    }

    // ── Expenses ──────────────────────────────────────────────────────────
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetExpensesAsync(from, to, category, page, pageSize, ct));

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateShopExpenseViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateExpenseAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateShopExpenseViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateExpenseAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("expenses/{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteExpenseAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    // ── Salaries ─────────────────────────────────────────────────────────
    [HttpGet("salaries")]
    public async Task<IActionResult> GetSalaries(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] Guid? staffId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetSalariesAsync(month, year, staffId, page, pageSize, ct));

    [HttpPost("salaries")]
    public async Task<IActionResult> RecordSalary([FromBody] RecordStaffSalaryViewModel vm, CancellationToken ct)
    {
        var result = await _service.RecordSalaryAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Attendance ────────────────────────────────────────────────────────
    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] Guid? staffId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetAttendanceAsync(staffId, from, to, page, pageSize, ct));

    [HttpPost("attendance")]
    [Authorize]   // all authenticated users can record attendance
    public async Task<IActionResult> RecordAttendance([FromBody] RecordAttendanceViewModel vm, CancellationToken ct)
    {
        var result = await _service.RecordAttendanceAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("attendance/summary")]
    public async Task<IActionResult> AttendanceSummary(
        [FromQuery] Guid staffId,
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken ct)
        => Ok(await _service.GetAttendanceSummaryAsync(staffId, month, year, ct));
}
