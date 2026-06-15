using Application.Common;
using Application.ViewModels.Finance;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IStaffAttendanceRepository : IBaseRepository<StaffAttendance>
{
    Task<PagedResult<AttendanceDetailViewModel>> GetPagedDetailAsync(
        Guid? staffId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default);

    Task<AttendanceSummaryViewModel> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default);
}
