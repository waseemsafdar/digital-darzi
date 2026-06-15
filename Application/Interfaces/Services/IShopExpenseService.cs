using Application.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IShopExpenseService
    : IBaseCrudService<CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel>
{
    Task<ApiResponse<PagedResult<ShopExpenseDetailViewModel>>> GetFilteredAsync(
        DateTime? from, DateTime? to, string? category,
        int page, int pageSize, CancellationToken ct = default);
}
