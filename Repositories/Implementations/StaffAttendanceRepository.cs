using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Finance;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class StaffAttendanceRepository : BaseRepository<StaffAttendance>, IStaffAttendanceRepository
{
    public StaffAttendanceRepository(ApplicationDbContext db) : base(db) { }

    protected override Task<IQueryable<StaffAttendance>> ApplyFiltersAsync(IQueryable<StaffAttendance> query, Application.ViewModels.Common.IBaseSearchModel search)
    {
        if (search is AttendanceSearchModel filter)
        {
            if (filter.StaffId.HasValue) query = query.Where(a => a.StaffUserId == filter.StaffId.Value);
            if (filter.From.HasValue)    query = query.Where(a => a.Date >= filter.From.Value);
            if (filter.To.HasValue)      query = query.Where(a => a.Date <= filter.To.Value);
        }
        return Task.FromResult(query);
    }

    public async Task<AttendanceSummaryViewModel> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var records = await _db.StaffAttendances.AsNoTracking()
            .Include(a => a.StaffUser)
            .Where(a => a.StaffUserId == staffId && a.Date.Month == month && a.Date.Year == year && !a.IsDeleted)
            .ToListAsync(ct);

        var staffName = records.FirstOrDefault()?.StaffUser?.Name ?? string.Empty;

        return new AttendanceSummaryViewModel
        {
            StaffUserId      = staffId,
            StaffName        = staffName,
            Month            = month,
            Year             = year,
            PresentDays      = records.Count(r => r.Status == AttendanceStatus.Present),
            AbsentDays       = records.Count(r => r.Status == AttendanceStatus.Absent),
            HalfDays         = records.Count(r => r.Status == AttendanceStatus.HalfDay),
            LeaveDays        = records.Count(r => r.Status == AttendanceStatus.Leave),
            TotalWorkingDays = records.Count
        };
    }
}
