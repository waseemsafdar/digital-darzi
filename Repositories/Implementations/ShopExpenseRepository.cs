using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Finance;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class ShopExpenseRepository : BaseRepository<ShopExpense>, IShopExpenseRepository
{
    public ShopExpenseRepository(ApplicationDbContext db) : base(db) { }

    protected override Task<IQueryable<ShopExpense>> ApplyFiltersAsync(IQueryable<ShopExpense> query, Application.ViewModels.Common.IBaseSearchModel search)
    {
        if (search is ShopExpenseSearchModel filter)
        {
            if (filter.From.HasValue) query = query.Where(e => e.ExpenseDate >= filter.From.Value);
            if (filter.To.HasValue)   query = query.Where(e => e.ExpenseDate <= filter.To.Value);
            if (!string.IsNullOrWhiteSpace(filter.Category))
                query = query.Where(e => e.Category == filter.Category);
        }
        return Task.FromResult(query);
    }
}
