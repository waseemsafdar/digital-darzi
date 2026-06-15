using Application.Common;
using Application.ViewModels.Common;
using Application.ViewModels.Customer;

namespace Application.Interfaces.Services;

public interface ICustomerService<TCreate, TUpdate, TDetail> : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default);
}
