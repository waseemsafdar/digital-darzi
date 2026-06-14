using Application.Common;
using Application.ViewModels.Finance;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IFinanceRepository
{
    // ── Expenses ───────────────────────────────────────────────────────────
    Task<PagedResult<ShopExpenseListViewModel>> GetExpensesAsync(DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default);
    Task<ShopExpense?> GetExpenseByIdAsync(Guid id, CancellationToken ct = default);
    Task<ShopExpense> CreateExpenseAsync(ShopExpense expense, CancellationToken ct = default);
    Task UpdateExpenseAsync(ShopExpense expense, CancellationToken ct = default);
    Task DeleteExpenseAsync(Guid id, CancellationToken ct = default);

    // ── Salaries ───────────────────────────────────────────────────────────
    Task<PagedResult<StaffSalaryListViewModel>> GetSalariesAsync(int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default);
    Task<StaffSalary?> GetSalaryByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffSalary> CreateSalaryAsync(StaffSalary salary, CancellationToken ct = default);

    // ── Attendance ─────────────────────────────────────────────────────────
    Task<PagedResult<AttendanceListViewModel>> GetAttendanceAsync(Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<StaffAttendance?> GetAttendanceByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffAttendance> RecordAttendanceAsync(StaffAttendance attendance, CancellationToken ct = default);
    Task UpdateAttendanceAsync(StaffAttendance attendance, CancellationToken ct = default);
    Task<AttendanceSummaryViewModel> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default);

    // ── Finance Summary ────────────────────────────────────────────────────
    Task<FinanceSummaryViewModel> GetFinanceSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
