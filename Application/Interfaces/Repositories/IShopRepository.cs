using Application.Common;
using Application.ViewModels.Shop;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IShopRepository : IBaseRepository<Shop>
{
    Task<PagedResult<ShopListViewModel>> GetListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ShopDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
}

