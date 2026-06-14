using Application.Common;
using Application.ViewModels.Customer;

namespace Application.Interfaces.Services;

public interface ICustomerService : IBaseCrudService<CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel>
{
    Task<ApiResponse<PagedResult<CustomerListViewModel>>> SearchAsync(CustomerSearchViewModel filter, CancellationToken ct = default);
    Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default);
}
