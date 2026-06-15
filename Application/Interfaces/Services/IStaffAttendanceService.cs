using Application.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IStaffAttendanceService
    : IBaseCrudService<RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel>
{
    Task<ApiResponse<PagedResult<AttendanceDetailViewModel>>> GetFilteredAsync(
        Guid? staffId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default);

    Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default);
}
