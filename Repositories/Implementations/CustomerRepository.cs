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
            var text = filter.Query ?? filter.Keyword;
            if (!string.IsNullOrWhiteSpace(text))
            {
                var pattern = Like.Contains(text);
                query = query.Where(c => EF.Functions.ILike(c.Name, pattern, "\\")
                    || EF.Functions.ILike(c.Phone, pattern, "\\")
                    || (c.WhatsAppNumber != null && EF.Functions.ILike(c.WhatsAppNumber, pattern, "\\")));
            }
            if (!string.IsNullOrWhiteSpace(filter.City))
                query = query.Where(c => c.City == filter.City);
            if (filter.Gender.HasValue)
                query = query.Where(c => c.Gender == filter.Gender.Value);
            if (filter.ActiveStatus.HasValue)
                query = query.Where(c => c.ActiveStatus == filter.ActiveStatus.Value);
            if (filter.CustomerType.HasValue)
                query = query.Where(c => c.CustomerType == filter.CustomerType.Value);
            if (filter.HasBalance == true)
                query = query.Where(c => _db.Orders.Any(o => o.CustomerId == c.Id && o.BalanceDue > 0 && o.Status != OrderStatus.Cancelled));
        }

        var sorted = (search.SortBy?.ToLowerInvariant(), string.Equals(search.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("name", true)        => query.OrderBy(c => c.Name),
            ("name", false)       => query.OrderByDescending(c => c.Name),
            ("totalspend", true)  => query.OrderBy(c => c.TotalSpend),
            ("totalspend", false) => query.OrderByDescending(c => c.TotalSpend),
            (_, true)             => query.OrderBy(c => c.CreatedOn),
            _                     => query.OrderByDescending(c => c.CreatedOn)
        };
        return Task.FromResult<IQueryable<Customer>>(sorted);
    }

    public async Task<bool> PhoneExistsAsync(string phone, Guid? excludeId, CancellationToken ct = default)
        => await _db.Customers.AnyAsync(c => c.Phone == phone && (excludeId == null || c.Id != excludeId), ct);

    public async Task<CustomerDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c == null ? null : new CustomerDetailViewModel
        {
            Id = c.Id, Name = c.Name, Phone = c.Phone, WhatsAppNumber = c.WhatsAppNumber, Email = c.Email,
            Address = c.Address, City = c.City, Gender = c.Gender, DateOfBirth = c.DateOfBirth, Notes = c.Notes,
            CustomerType = c.CustomerType, PreferredKarigarId = c.PreferredKarigarId, PreferredStyles = c.PreferredStyles,
            NotificationsOptOut = c.NotificationsOptOut, TotalOrders = c.TotalOrders, TotalSpend = c.TotalSpend,
            LoyaltyPoints = c.LoyaltyPoints, LoyaltyTier = c.LoyaltyTier, ActiveStatus = c.ActiveStatus, CreatedOn = c.CreatedOn
        };
    }

    /// <summary>
    /// The customer's account, built from source lines rather than editable totals:
    /// order charges (subtotal − discount), alteration charges and cancellations are debits/credits,
    /// payments are credits and refunds are debits. Balance = Σ debit − Σ credit.
    /// </summary>
    public async Task<CustomerLedgerViewModel?> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer == null) return null;

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.CreatedOn)
            .Take(500)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CreatedOn, o.CancelledOn, o.SubTotal, o.Discount, o.GrandTotal, o.AmountPaid,
                o.BalanceDue, o.Status, o.DeliveryDate
            })
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();
        var payments = await _db.OrderPayments.AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .Select(p => new { p.OrderId, p.Amount, p.PaidAt, p.IsRefund, p.Note, p.PaymentMethod })
            .ToListAsync(ct);
        var alterations = await _db.OrderAlterations.AsNoTracking()
            .Where(a => orderIds.Contains(a.OrderId) && a.AdditionalCharge > 0 && a.Status != AlterationStatus.Cancelled)
            .Select(a => new { a.OrderId, a.AdditionalCharge, a.CreatedOn, a.Description })
            .ToListAsync(ct);

        var numbers = orders.ToDictionary(o => o.Id, o => o.OrderNumber);
        var entries = new List<CustomerLedgerEntryViewModel>();
        foreach (var o in orders)
        {
            entries.Add(new() { Date = o.CreatedOn, Type = "Order", OrderId = o.Id, OrderNumber = o.OrderNumber,
                Description = "Order placed", Debit = o.SubTotal - o.Discount });
            if (o.Status == OrderStatus.Cancelled)
            {
                var netPaid = payments.Where(p => p.OrderId == o.Id).Sum(p => p.Amount);
                var unpaidCharge = o.SubTotal - o.Discount
                    + alterations.Where(a => a.OrderId == o.Id).Sum(a => a.AdditionalCharge) - netPaid;
                if (unpaidCharge != 0)
                    entries.Add(new() { Date = o.CancelledOn ?? o.CreatedOn, Type = "Cancellation", OrderId = o.Id,
                        OrderNumber = o.OrderNumber, Description = "Order cancelled", Credit = unpaidCharge });
            }
        }
        entries.AddRange(alterations.Select(a => new CustomerLedgerEntryViewModel
        {
            Date = a.CreatedOn, Type = "Alteration", OrderId = a.OrderId, OrderNumber = numbers[a.OrderId],
            Description = a.Description, Debit = a.AdditionalCharge
        }));
        entries.AddRange(payments.Select(p => new CustomerLedgerEntryViewModel
        {
            Date = p.PaidAt, Type = p.IsRefund ? "Refund" : "Payment", OrderId = p.OrderId, OrderNumber = numbers[p.OrderId],
            Description = p.Note ?? p.PaymentMethod.ToString(),
            Debit = p.IsRefund ? -p.Amount : 0,
            Credit = p.IsRefund ? 0 : p.Amount
        }));

        var running = 0m;
        foreach (var e in entries.OrderBy(e => e.Date).ThenBy(e => e.Type == "Order" ? 0 : 1))
        {
            running += e.Debit - e.Credit;
            e.RunningBalance = running;
        }

        var active = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        return new CustomerLedgerViewModel
        {
            CustomerId = customer.Id,
            Name       = customer.Name,
            Phone      = customer.Phone,
            Orders     = orders.Select(o => new CustomerLedgerOrderViewModel
            {
                OrderId = o.Id, OrderNumber = o.OrderNumber, GrandTotal = o.GrandTotal, AmountPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue, Status = o.Status.ToString(), DeliveryDate = o.DeliveryDate
            }).ToList(),
            Entries    = entries.OrderBy(e => e.Date).ToList(),
            TotalGrand = active.Sum(o => o.GrandTotal),
            TotalPaid  = active.Sum(o => o.AmountPaid),
            TotalDue   = active.Sum(o => o.BalanceDue)
        };
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _dbSet.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return;
        c.IsDeleted = true;
        c.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }
}
