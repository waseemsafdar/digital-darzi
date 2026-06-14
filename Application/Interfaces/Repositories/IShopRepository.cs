using Application.Common;
using Application.ViewModels.Shop;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ShopListViewModel>> GetListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ShopDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<Shop> CreateAsync(Shop shop, CancellationToken ct = default);
    Task UpdateAsync(Shop shop, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
