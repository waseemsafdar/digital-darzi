using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize]
public class FinanceController : BaseController
{
    private readonly IFinanceService _service;
    private readonly TimeProvider _clock;

    public FinanceController(IFinanceService service, TimeProvider clock)
    {
        _service = service;
        _clock = clock;
    }

    /// <summary>Cash-basis summary for [from, to] (defaults to the last 30 days).</summary>
    [HttpGet("summary")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return ReturnProcessedResponse(await _service.GetSummaryAsync(from ?? now.AddDays(-30), to ?? now, ct));
    }

    // ── Expenses ──────────────────────────────────────────────────────────
    [HttpGet("expenses")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetExpenses([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetExpensesAsync(from, to, category, page, pageSize, ct));

    [HttpPost("expenses")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateShopExpenseViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateExpenseAsync(vm, ct));

    [HttpPut("expenses/{id}")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateShopExpenseViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateExpenseAsync(id, vm, ct));

    [HttpDelete("expenses/{id}")]
    [HasPermission(Permissions.FinanceDelete)]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteExpenseAsync(id, ct));

    // ── Expense categories ────────────────────────────────────────────────
    [HttpGet("expense-categories")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetCategories(CancellationToken ct) => ReturnProcessedResponse(await _service.GetCategoriesAsync(ct));

    [HttpPost("expense-categories")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> CreateCategory([FromBody] SaveExpenseCategoryViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CreateCategoryAsync(vm, ct));

    [HttpPut("expense-categories/{id}")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] SaveExpenseCategoryViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateCategoryAsync(id, vm, ct));

    [HttpDelete("expense-categories/{id}")]
    [HasPermission(Permissions.FinanceDelete)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteCategoryAsync(id, ct));

    // ── Salaries ─────────────────────────────────────────────────────────
    [HttpGet("salaries")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetSalaries([FromQuery] int? month, [FromQuery] int? year, [FromQuery] Guid? staffId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetSalariesAsync(month, year, staffId, page, pageSize, ct));

    [HttpPost("salaries")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> RecordSalary([FromBody] RecordStaffSalaryViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordSalaryAsync(vm, ct));

    /// <summary>Pay (part of) the remaining amount on a salary record.</summary>
    [HttpPost("salaries/{id}/payments")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> RecordSalaryPayment(Guid id, [FromBody] RecordSalaryPaymentViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordSalaryPaymentAsync(id, vm, ct));

    [HttpGet("salary-rules")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetSalaryRules(CancellationToken ct) => ReturnProcessedResponse(await _service.GetSalaryRulesAsync(ct));

    [HttpPut("salary-rules")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> SaveSalaryRule([FromBody] SaveSalaryRuleViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveSalaryRuleAsync(vm, ct));

    /// <summary>Preview a month's salary from attendance, the salary rule and outstanding advances.</summary>
    [HttpGet("salaries/compute")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> ComputeSalary([FromQuery] Guid staffId, [FromQuery] int month, [FromQuery] int year, CancellationToken ct)
        => ReturnProcessedResponse(await _service.ComputeSalaryAsync(staffId, month, year, ct));

    /// <summary>Record the computed salary (optionally recovering advances).</summary>
    [HttpPost("salaries/compute")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> RecordComputedSalary([FromBody] RecordComputedSalaryViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordComputedSalaryAsync(vm, ct));

    [HttpGet("salary-advances")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetAdvances([FromQuery] Guid? staffId, [FromQuery] bool outstandingOnly = false, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetAdvancesAsync(staffId, outstandingOnly, ct));

    [HttpPost("salary-advances")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> RecordAdvance([FromBody] RecordSalaryAdvanceViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordAdvanceAsync(vm, ct));

    // ── Attendance ────────────────────────────────────────────────────────
    [HttpGet("attendance")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetAttendance([FromQuery] Guid? staffId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetAttendanceAsync(staffId, from, to, page, pageSize, ct));

    /// <summary>Record attendance for any staff member (supervisors). Staff punch in via /api/attendance/check-in.</summary>
    [HttpPost("attendance")]
    [HasPermission(Permissions.FinanceManage)]
    public async Task<IActionResult> RecordAttendance([FromBody] RecordAttendanceViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordAttendanceAsync(vm, ct));

    [HttpGet("attendance/summary")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> AttendanceSummary([FromQuery] Guid staffId, [FromQuery] int month, [FromQuery] int year, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetAttendanceSummaryAsync(staffId, month, year, ct));
}
