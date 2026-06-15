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

    public async Task<PagedResult<ShopExpenseDetailViewModel>> GetPagedDetailAsync(
        DateTime? from, DateTime? to, string? category,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.ShopExpenses.AsNoTracking()
            .Include(e => e.AddedByUser)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (from.HasValue)   query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue)     query = query.Where(e => e.ExpenseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ShopExpenseDetailViewModel
            {
                Id            = e.Id,
                ShopId        = e.ShopId,
                Category      = e.Category,
                Description   = e.Description,
                Amount        = e.Amount,
                ExpenseDate   = e.ExpenseDate,
                PaymentMethod = e.PaymentMethod,
                ReceiptRef    = e.ReceiptRef,
                AddedByUserId = e.AddedByUserId,
                AddedByName   = e.AddedByUser != null ? e.AddedByUser.Name : string.Empty,
                CreatedOn     = e.CreatedOn
            })
            .ToListAsync(ct);

        return PagedResult<ShopExpenseDetailViewModel>.From(items, total, page, pageSize);
    }
}
