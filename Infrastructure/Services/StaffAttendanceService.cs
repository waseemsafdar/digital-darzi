using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Infrastructure.Services;

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

    public StaffAttendanceService(
        IStaffAttendanceRepository repo,
        ICurrentUserService currentUser,
        IMapper mapper,
        ApplicationDbContext db)
        : base(repo, mapper)
    {
        _attendanceRepo = repo;
        _currentUser    = currentUser;
        _db             = db;
    }

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var result = await _attendanceRepo.GetSummaryAsync(staffId, month, year, ct);
        return new ApiResponse<AttendanceSummaryViewModel>(result);
    }

    public async Task<ApiResponse<List<AttendanceListViewModel>>> GetMyAttendanceAsync(
        DateTime? from, DateTime? to, int? month, int? year, CancellationToken ct = default)
    {
        var staffId = _currentUser.UserId;
        var query   = _db.StaffAttendances.AsNoTracking()
                         .Include(a => a.StaffUser)
                         .Where(a => a.StaffUserId == staffId && !a.IsDeleted);

        if (from.HasValue)
            query = query.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(a => a.Date <= to.Value.Date);

        // If no date range, default to the supplied month/year (or current month)
        if (!from.HasValue && !to.HasValue)
        {
            var m = month ?? DateTime.Today.Month;
            var y = year  ?? DateTime.Today.Year;
            query = query.Where(a => a.Date.Month == m && a.Date.Year == y);
        }

        var records = await query.OrderByDescending(a => a.Date).ToListAsync(ct);

        var items = records.Select(a => new AttendanceListViewModel
        {
            Id          = a.Id,
            StaffUserId = a.StaffUserId,
            StaffName   = a.StaffUser?.Name ?? string.Empty,
            Date        = a.Date,
            Status      = a.Status,
            CheckIn     = a.CheckIn,
            CheckOut    = a.CheckOut,
            Notes       = a.Notes
        }).ToList();

        return ApiResponse<List<AttendanceListViewModel>>.Ok(items);
    }

    public async Task<ApiResponse<Guid>> CheckInAsync(StaffCheckInViewModel vm, CancellationToken ct = default)
    {
        var staffId = _currentUser.UserId;
        var today   = DateTime.Today;
        var now     = DateTime.UtcNow;

        // Look for an existing record for today
        var existing = await _db.StaffAttendances
            .FirstOrDefaultAsync(a => a.StaffUserId == staffId && a.Date == today && !a.IsDeleted, ct);

        if (existing != null)
        {
            // Already checked in — update CheckOut if provided, otherwise treat as a re-submit
            if (vm.CheckOut.HasValue)
                existing.CheckOut = vm.CheckOut;
            if (vm.Notes != null)
                existing.Notes = vm.Notes;
            existing.UpdatedBy = staffId;
            existing.UpdatedOn = now;
            await _db.SaveChangesAsync(ct);
            return ApiResponse<Guid>.Ok(existing.Id, "Attendance updated (check-out recorded).");
        }

        // Create new check-in record
        var checkInTime = TimeSpan.FromHours(now.Hour).Add(TimeSpan.FromMinutes(now.Minute));
        var attendance = new StaffAttendance
        {
            Id           = Guid.NewGuid(),
            TenantId     = _currentUser.TenantId,
            BranchId     = _currentUser.ShopId,
            ShopId       = _currentUser.ShopId,
            StaffUserId  = staffId,
            Date         = today,
            Status       = AttendanceStatus.Present,
            CheckIn      = checkInTime,
            CheckOut     = vm.CheckOut,
            Notes        = vm.Notes,
            ActiveStatus = ActiveStatus.Active,
            CreatedBy    = staffId,
            CreatedOn    = now,
            UpdatedBy    = staffId,
            UpdatedOn    = now
        };

        _db.StaffAttendances.Add(attendance);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(attendance.Id, "Checked in successfully.");
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(
        TCreate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = _mapper.Map<StaffAttendance>(vm);
            entity.ShopId    = _currentUser.ShopId;
            entity.CreatedBy = _currentUser.UserId;
            entity.CreatedOn = DateTime.UtcNow;
            entity.ActiveStatus = Domain.Enums.ActiveStatus.Active;
            await _repo.AddAsync(entity, ct);
            return new ApiResponse<Guid>(entity.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while creating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(
        TUpdate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(vm.Id, ct);
            if (entity == null) return new ApiResponse<Guid>("Attendance record not found.", 404);
            _mapper.Map(vm, entity);
            entity.UpdatedBy = _currentUser.UserId;
            entity.UpdatedOn = DateTime.UtcNow;
            await _repo.UpdateAsync(entity, ct);
            return new ApiResponse<Guid>(entity.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while updating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }
}
