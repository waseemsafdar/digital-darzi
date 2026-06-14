using Application.Common;
using Application.ViewModels.Shop;

namespace Application.Interfaces.Services;

public interface IShopService : IBaseCrudService<CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel>
{
    Task<ApiResponse<PagedResult<ShopListViewModel>>> GetListAsync(int page, int pageSize, CancellationToken ct = default);
}
