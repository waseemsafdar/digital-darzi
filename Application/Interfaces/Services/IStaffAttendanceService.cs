using Application.Common;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IStaffAttendanceService<TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default);

    /// <summary>Returns the logged-in staff member's own attendance records, filtered by date range or month/year.</summary>
    Task<ApiResponse<List<AttendanceListViewModel>>> GetMyAttendanceAsync(
        DateTime? from, DateTime? to, int? month, int? year, CancellationToken ct = default);

    /// <summary>Self-service check-in: creates today's attendance record as Present with CheckIn = now.
    /// If a record already exists for today, updates CheckOut instead.</summary>
    Task<ApiResponse<Guid>> CheckInAsync(StaffCheckInViewModel vm, CancellationToken ct = default);
}
