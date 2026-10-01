using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Attendance for <c>/api/attendance</c>. Dates and check-in times use the branch's time zone
/// (previously UTC, so late check-ins in Pakistan landed on the wrong day).
/// </summary>
public class StaffAttendanceService<TCreate, TUpdate, TDetail>
    : BaseCrudService<StaffAttendance, TCreate, TUpdate, TDetail>,
      IStaffAttendanceService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly IStaffAttendanceRepository _attendanceRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;

    public StaffAttendanceService(IStaffAttendanceRepository repo, ICurrentUserService currentUser, IObjectMapper mapper,
        ApplicationDbContext db, IBusinessCalendar calendar)
        : base(repo, mapper)
    {
        _attendanceRepo = repo;
        _currentUser    = currentUser;
        _db             = db;
        _calendar       = calendar;
    }

    protected override string EntityName => "Attendance record";

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default)
        => ApiResponse<AttendanceSummaryViewModel>.Ok(await _attendanceRepo.GetSummaryAsync(staffId, month, year, ct));

    public async Task<ApiResponse<List<AttendanceListViewModel>>> GetMyAttendanceAsync(
        DateTime? from, DateTime? to, int? month, int? year, CancellationToken ct = default)
    {
        var staffId = _currentUser.UserId;
        var query = _db.StaffAttendances.AsNoTracking().Where(a => a.StaffUserId == staffId);

        if (from.HasValue) query = query.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue)   query = query.Where(a => a.Date <= to.Value.Date);
        if (!from.HasValue && !to.HasValue)
        {
            var today = await _calendar.TodayAsync(ct);
            var start = new DateTime(year ?? today.Year, month ?? today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(a => a.Date >= start && a.Date < start.AddMonths(1));
        }

        var items = await query
            .OrderByDescending(a => a.Date)
            .Take(400)
            .Select(a => new AttendanceListViewModel
            {
                Id = a.Id, StaffUserId = a.StaffUserId, StaffName = a.StaffUser != null ? a.StaffUser.Name : string.Empty,
                Date = a.Date, Status = a.Status, CheckIn = a.CheckIn, CheckOut = a.CheckOut, Notes = a.Notes
            })
            .ToListAsync(ct);
        return ApiResponse<List<AttendanceListViewModel>>.Ok(items);
    }

    /// <summary>
    /// Self-service punch. First call today creates a Present record with the shop-local check-in
    /// time; a later call records the check-out (the supplied time, or now).
    /// </summary>
    public async Task<ApiResponse<Guid>> CheckInAsync(StaffCheckInViewModel vm, CancellationToken ct = default)
    {
        var staffId = _currentUser.UserId;
        var today   = await _calendar.TodayAsync(ct);
        var nowTime = await _calendar.LocalTimeOfDayAsync(ct);

        var existing = await _db.StaffAttendances.FirstOrDefaultAsync(a => a.StaffUserId == staffId && a.Date == today, ct);
        if (existing != null)
        {
            existing.CheckOut = vm.CheckOut ?? nowTime;
            if (vm.Notes != null) existing.Notes = vm.Notes;
            await _db.SaveChangesAsync(ct);
            return ApiResponse<Guid>.Ok(existing.Id, "Attendance updated (check-out recorded).");
        }

        var attendance = new StaffAttendance
        {
            StaffUserId  = staffId,
            Date         = today,
            Status       = AttendanceStatus.Present,
            CheckIn      = nowTime,
            CheckOut     = vm.CheckOut,
            Notes        = vm.Notes,
            ActiveStatus = ActiveStatus.Active
        };
        _db.StaffAttendances.Add(attendance);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(attendance.Id, "Checked in successfully.");
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        if (vm is RecordAttendanceViewModel r)
        {
            if (!await _db.AppUsers.AnyAsync(u => u.Id == r.StaffUserId, ct))
                return ApiResponse<Guid>.NotFound("Staff member not found.");
            var date = await _calendar.ToCalendarDateAsync(r.Date, ct);
            if (await _db.StaffAttendances.AnyAsync(a => a.StaffUserId == r.StaffUserId && a.Date == date, ct))
                return ApiResponse<Guid>.Conflict($"Attendance for {date:yyyy-MM-dd} is already recorded for this staff member.");
        }
        return await base.CreateAsync(vm, ct);
    }

    protected override async Task BeforeCreateAsync(StaffAttendance entity, TCreate vm, CancellationToken ct)
        => entity.Date = await _calendar.ToCalendarDateAsync(entity.Date, ct);
}
