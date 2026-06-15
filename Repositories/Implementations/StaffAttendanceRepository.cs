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

    public async Task<PagedResult<AttendanceDetailViewModel>> GetPagedDetailAsync(
        Guid? staffId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.StaffAttendances.AsNoTracking()
            .Include(a => a.StaffUser)
            .Where(a => !a.IsDeleted)
            .AsQueryable();

        if (staffId.HasValue) query = query.Where(a => a.StaffUserId == staffId.Value);
        if (from.HasValue)    query = query.Where(a => a.Date >= from.Value);
        if (to.HasValue)      query = query.Where(a => a.Date <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceDetailViewModel
            {
                Id          = a.Id,
                ShopId      = a.ShopId,
                StaffUserId = a.StaffUserId,
                StaffName   = a.StaffUser != null ? a.StaffUser.Name : string.Empty,
                Date        = a.Date,
                Status      = a.Status,
                CheckIn     = a.CheckIn,
                CheckOut    = a.CheckOut,
                Notes       = a.Notes,
                CreatedOn   = a.CreatedOn
            })
            .ToListAsync(ct);

        return PagedResult<AttendanceDetailViewModel>.From(items, total, page, pageSize);
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
