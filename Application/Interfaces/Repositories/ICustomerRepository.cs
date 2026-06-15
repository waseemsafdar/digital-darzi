using Application.Common;
using Application.ViewModels.Customer;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface ICustomerRepository : IBaseRepository<Customer>
{
    Task<CustomerDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<CustomerLedgerViewModel?> GetLedgerAsync(Guid id, CancellationToken ct = default);
}
