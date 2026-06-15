using Application.ViewModels.Common;

namespace Application.Interfaces.Services;

public interface IShopExpenseService<TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
}
