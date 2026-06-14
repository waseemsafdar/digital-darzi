using Application.Common;
using Application.ViewModels.Customer;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CustomerListViewModel>> SearchAsync(CustomerSearchViewModel filter, CancellationToken ct = default);
    Task<CustomerDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<CustomerLedgerViewModel?> GetLedgerAsync(Guid id, CancellationToken ct = default);
    Task<Customer> CreateAsync(Customer customer, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
