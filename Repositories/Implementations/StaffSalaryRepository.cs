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

    protected override IQueryable<StaffSalary> GetBaseQuery()
    {
        return _dbSet.AsNoTracking()
            .Include(s => s.StaffUser)
            .Where(x => !x.IsDeleted);
    }

    protected override Task<IQueryable<StaffSalary>> ApplyFiltersAsync(IQueryable<StaffSalary> query, Application.ViewModels.Common.IBaseSearchModel search)
    {
        if (search is StaffSalarySearchModel model)
        {
            if (model.Month.HasValue)   query = query.Where(s => s.Month == model.Month.Value);
            if (model.Year.HasValue)    query = query.Where(s => s.Year == model.Year.Value);
            if (model.StaffId.HasValue) query = query.Where(s => s.StaffUserId == model.StaffId.Value);
        }

        query = query.OrderByDescending(s => s.Year).ThenByDescending(s => s.Month);

        return Task.FromResult(query);
    }
}
