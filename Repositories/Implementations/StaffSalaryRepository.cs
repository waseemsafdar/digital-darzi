using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Finance;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class StaffSalaryRepository : BaseRepository<StaffSalary>, IStaffSalaryRepository
{
    public StaffSalaryRepository(ApplicationDbContext db) : base(db) { }

    public async Task<PagedResult<StaffSalaryDetailViewModel>> GetPagedDetailAsync(
        int? month, int? year, Guid? staffId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.StaffSalaries.AsNoTracking()
            .Include(s => s.StaffUser)
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (month.HasValue)   query = query.Where(s => s.Month == month.Value);
        if (year.HasValue)    query = query.Where(s => s.Year == year.Value);
        if (staffId.HasValue) query = query.Where(s => s.StaffUserId == staffId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StaffSalaryDetailViewModel
            {
                Id            = s.Id,
                ShopId        = s.ShopId,
                StaffUserId   = s.StaffUserId,
                StaffName     = s.StaffUser != null ? s.StaffUser.Name : string.Empty,
                Month         = s.Month,
                Year          = s.Year,
                BaseSalary    = s.BaseSalary,
                Bonus         = s.Bonus,
                Deduction     = s.Deduction,
                NetSalary     = s.NetSalary,
                PaymentMethod = s.PaymentMethod,
                Notes         = s.Notes,
                PaidOn        = s.PaidOn
            })
            .ToListAsync(ct);

        return PagedResult<StaffSalaryDetailViewModel>.From(items, total, page, pageSize);
    }
}
