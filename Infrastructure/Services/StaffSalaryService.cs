using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>Generic CRUD for <c>/api/salaries</c>. Net and payment status are always derived.</summary>
public class StaffSalaryService<TCreate, TUpdate, TDetail>
    : BaseCrudService<StaffSalary, TCreate, TUpdate, TDetail>,
      IStaffSalaryService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;

    public StaffSalaryService(IStaffSalaryRepository repo, IObjectMapper mapper, ApplicationDbContext db, IBusinessCalendar calendar)
        : base(repo, mapper)
    {
        _db = db;
        _calendar = calendar;
    }

    protected override string EntityName => "Salary record";

    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        if (vm is RecordStaffSalaryViewModel s)
        {
            if (!await _db.AppUsers.AnyAsync(u => u.Id == s.StaffUserId, ct))
                return ApiResponse<Guid>.NotFound("Staff member not found.");
            if (await _db.StaffSalaries.AnyAsync(x => x.StaffUserId == s.StaffUserId && x.Month == s.Month && x.Year == s.Year, ct))
                return ApiResponse<Guid>.Conflict($"A salary for {s.Month}/{s.Year} is already recorded for this staff member.");
            var net = s.BaseSalary + s.Bonus - s.Deduction;
            if (s.AmountPaid is < 0 || s.AmountPaid > net)
                return ApiResponse<Guid>.Fail($"Amount paid must be between 0 and the net salary ({net:N2}).");
        }
        return await base.CreateAsync(vm, ct);
    }

    protected override Task BeforeCreateAsync(StaffSalary entity, TCreate vm, CancellationToken ct)
    {
        entity.PaidOn = _calendar.UtcNow;
        entity.Status = FinanceService.SalaryStatusFor(entity.NetSalary, entity.AmountPaid);
        return Task.CompletedTask;
    }

    protected override Task BeforeUpdateAsync(StaffSalary entity, TUpdate vm, CancellationToken ct)
    {
        entity.NetSalary = entity.BaseSalary + entity.Bonus - entity.Deduction;
        if (entity.AmountPaid > entity.NetSalary) entity.AmountPaid = entity.NetSalary;
        entity.Status = FinanceService.SalaryStatusFor(entity.NetSalary, entity.AmountPaid);
        return Task.CompletedTask;
    }
}
