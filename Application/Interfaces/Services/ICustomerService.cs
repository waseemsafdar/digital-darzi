using Application.Common;
using Application.ViewModels.Customer;

namespace Application.Interfaces.Services;

public interface ICustomerService
{
    Task<ApiResponse<PagedResult<CustomerListViewModel>>> SearchAsync(CustomerSearchViewModel filter, CancellationToken ct = default);
    Task<ApiResponse<CustomerDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<CustomerDetailViewModel>> CreateAsync(CreateCustomerViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<CustomerDetailViewModel>> UpdateAsync(Guid id, UpdateCustomerViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default);
}
