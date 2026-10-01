using Application.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IFinanceService
{
    // Expenses
    Task<ApiResponse<PagedResult<ShopExpenseListViewModel>>> GetExpensesAsync(DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<ShopExpenseListViewModel>> CreateExpenseAsync(CreateShopExpenseViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<ShopExpenseListViewModel>> UpdateExpenseAsync(Guid id, UpdateShopExpenseViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteExpenseAsync(Guid id, CancellationToken ct = default);

    // Expense categories
    Task<ApiResponse<List<ExpenseCategoryViewModel>>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ApiResponse<ExpenseCategoryViewModel>> CreateCategoryAsync(SaveExpenseCategoryViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<ExpenseCategoryViewModel>> UpdateCategoryAsync(Guid id, SaveExpenseCategoryViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteCategoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>Null when the category is acceptable for this branch; otherwise the reason.</summary>
    Task<string?> ValidateCategoryAsync(string category, CancellationToken ct = default);

    // Salaries
    Task<ApiResponse<PagedResult<StaffSalaryListViewModel>>> GetSalariesAsync(int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryAsync(RecordStaffSalaryViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryPaymentAsync(Guid salaryId, RecordSalaryPaymentViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<List<SalaryRuleViewModel>>> GetSalaryRulesAsync(CancellationToken ct = default);
    Task<ApiResponse<SalaryRuleViewModel>> SaveSalaryRuleAsync(SaveSalaryRuleViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<SalaryComputationViewModel>> ComputeSalaryAsync(Guid staffId, int month, int year, CancellationToken ct = default);
    Task<ApiResponse<StaffSalaryListViewModel>> RecordComputedSalaryAsync(RecordComputedSalaryViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<List<SalaryAdvanceViewModel>>> GetAdvancesAsync(Guid? staffId, bool outstandingOnly, CancellationToken ct = default);
    Task<ApiResponse<SalaryAdvanceViewModel>> RecordAdvanceAsync(RecordSalaryAdvanceViewModel vm, CancellationToken ct = default);

    // Attendance
    Task<ApiResponse<PagedResult<AttendanceListViewModel>>> GetAttendanceAsync(Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<AttendanceListViewModel>> RecordAttendanceAsync(RecordAttendanceViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<AttendanceSummaryViewModel>> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default);

    // Summary
    Task<ApiResponse<FinanceSummaryViewModel>> GetSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
