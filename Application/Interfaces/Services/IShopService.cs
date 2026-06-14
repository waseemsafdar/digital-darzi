using Application.Common;
using Application.ViewModels.Shop;

namespace Application.Interfaces.Services;

public interface IShopService
{
    Task<ApiResponse<PagedResult<ShopListViewModel>>> GetListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<ShopDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<ShopDetailViewModel>> CreateAsync(CreateShopViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<ShopDetailViewModel>> UpdateAsync(Guid id, UpdateShopViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default);
}
