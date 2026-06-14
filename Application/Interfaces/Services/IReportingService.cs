using Application.Common;
using Application.ViewModels.Reporting;

namespace Application.Interfaces.Services;

public interface IReportingService
{
    Task<ApiResponse<DashboardSummaryViewModel>> GetDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<ApiResponse<RevenueReportViewModel>> GetRevenueReportAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<ApiResponse<List<TopCustomerViewModel>>> GetTopCustomersAsync(DateTime from, DateTime to, int limit, CancellationToken ct = default);
    Task<ApiResponse<List<StaffPerformanceViewModel>>> GetStaffPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<ApiResponse<OrderStatusReportViewModel>> GetOrderStatusReportAsync(CancellationToken ct = default);
    Task<ApiResponse<List<PendingBalanceViewModel>>> GetPendingBalancesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<DailyRevenueViewModel>>> GetDailyRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
