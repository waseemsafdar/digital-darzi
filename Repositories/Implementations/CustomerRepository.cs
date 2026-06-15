using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Customer;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(ApplicationDbContext db) : base(db) { }

    protected override Task<IQueryable<Customer>> ApplyFiltersAsync(IQueryable<Customer> query, Application.ViewModels.Common.IBaseSearchModel search)
    {
        if (search is CustomerSearchViewModel filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                var q = filter.Query.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(q) || c.Phone.Contains(q));
            }
            if (!string.IsNullOrWhiteSpace(filter.City))
                query = query.Where(c => c.City == filter.City);
            if (filter.Gender.HasValue)
                query = query.Where(c => c.Gender == filter.Gender.Value);
            if (filter.ActiveStatus.HasValue)
                query = query.Where(c => c.ActiveStatus == filter.ActiveStatus.Value);
        }
        return Task.FromResult(query);
    }

    public async Task<CustomerDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return null;

        return new CustomerDetailViewModel
        {
            Id           = c.Id,
            Name         = c.Name,
            Phone        = c.Phone,
            Email        = c.Email,
            Address      = c.Address,
            City         = c.City,
            Gender       = c.Gender,
            DateOfBirth  = c.DateOfBirth,
            Notes        = c.Notes,
            TotalOrders  = c.TotalOrders,
            TotalSpend   = c.TotalSpend,
            LoyaltyPoints= c.LoyaltyPoints,
            LoyaltyTier  = c.LoyaltyTier,
            ActiveStatus = c.ActiveStatus,
            CreatedOn    = c.CreatedOn
        };
    }

    public async Task<CustomerLedgerViewModel?> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer == null) return null;

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => new CustomerLedgerOrderViewModel
            {
                OrderId     = o.Id,
                OrderNumber = o.OrderNumber,
                GrandTotal  = o.GrandTotal,
                AmountPaid  = o.AmountPaid,
                BalanceDue  = o.BalanceDue,
                Status      = o.Status.ToString(),
                DeliveryDate= o.DeliveryDate
            })
            .ToListAsync(ct);

        return new CustomerLedgerViewModel
        {
            CustomerId  = customer.Id,
            Name        = customer.Name,
            Phone       = customer.Phone,
            Orders      = orders,
            TotalGrand  = orders.Sum(o => o.GrandTotal),
            TotalPaid   = orders.Sum(o => o.AmountPaid),
            TotalDue    = orders.Sum(o => o.BalanceDue)
        };
    }

    // Override soft delete to also mark ActiveStatus
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await GetByIdAsync(id, ct);
        if (c == null) return;
        c.IsDeleted = true;
        c.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }
}
