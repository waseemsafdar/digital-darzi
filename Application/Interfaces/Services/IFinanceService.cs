using Application.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IFinanceService
{
    Task<ApiResponse<PagedResult<ShopExpenseListViewModel>>> GetExpensesAsync(DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<ShopExpenseListViewModel>> CreateExpenseAsync(CreateShopExpenseViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<ShopExpenseListViewModel>> UpdateExpenseAsync(Guid id, UpdateShopExpenseViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteExpenseAsync(Guid id, CancellationToken ct = default);

    Task<ApiResponse<PagedResult<StaffSalaryListViewModel>>> GetSalariesAsync(int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryAsync(RecordStaffSalaryViewModel vm, CancellationToken ct = default);

    Task<ApiResponse<PagedResult<AttendanceListViewModel>>> GetAttendanceAsync(Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<AttendanceListViewModel>> RecordAttendanceAsync(RecordAttendanceViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<AttendanceSummaryViewModel>> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default);

    Task<ApiResponse<FinanceSummaryViewModel>> GetSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
