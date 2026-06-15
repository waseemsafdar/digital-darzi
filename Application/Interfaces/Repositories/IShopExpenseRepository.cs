using Application.Common;
using Application.ViewModels.Finance;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IShopExpenseRepository : IBaseRepository<ShopExpense>
{
    Task<PagedResult<ShopExpenseDetailViewModel>> GetPagedDetailAsync(
        DateTime? from, DateTime? to, string? category,
        int page, int pageSize, CancellationToken ct = default);
}
