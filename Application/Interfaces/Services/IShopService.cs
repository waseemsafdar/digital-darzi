using Application.Common;
using Application.ViewModels.Common;

namespace Application.Interfaces.Services;

public interface IShopService<TCreate, TUpdate, TDetail> : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    Task<ApiResponse<bool>> UpdateSubscriptionAsync(Guid id, Application.ViewModels.Shop.UpdateShopSubscriptionRequest request, CancellationToken ct);
}

