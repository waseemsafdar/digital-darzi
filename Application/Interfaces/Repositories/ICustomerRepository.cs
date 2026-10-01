using Application.ViewModels.Customer;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface ICustomerRepository : IBaseRepository<Customer>
{
    Task<CustomerDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<CustomerLedgerViewModel?> GetLedgerAsync(Guid id, CancellationToken ct = default);

    /// <summary>True when another customer in this branch already uses <paramref name="phone"/>.</summary>
    Task<bool> PhoneExistsAsync(string phone, Guid? excludeId, CancellationToken ct = default);
}
