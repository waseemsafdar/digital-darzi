using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Repositories.Implementations;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _db;
    public OrderRepository(ApplicationDbContext db) => _db = db;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.StageAssignments)
            .Include(o => o.Payments)
            .Include(o => o.Alterations)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<OrderDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _db.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items)
                .ThenInclude(i => i.StageLogs)
                    .ThenInclude(l => l.Karigar)
            .Include(x => x.Items)
                .ThenInclude(i => i.StageAssignments)
                    .ThenInclude(a => a.AssignedKarigar)
            .Include(x => x.Payments)
            .Include(x => x.Alterations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (o == null) return null;

        return new OrderDetailViewModel
        {
            Id                  = o.Id,
            OrderNumber         = o.OrderNumber,
            CustomerId          = o.CustomerId,
            CustomerName        = o.Customer?.Name ?? string.Empty,
            CustomerPhone       = o.Customer?.Phone ?? string.Empty,
            DeliveryDate        = o.DeliveryDate,
            Notes               = o.Notes,
            SpecialInstructions = o.SpecialInstructions,
            SubTotal            = o.SubTotal,
            Discount            = o.Discount,
            GrandTotal          = o.GrandTotal,
            AmountPaid          = o.AmountPaid,
            BalanceDue          = o.BalanceDue,
            Status              = o.Status,
            CreatedOn           = o.CreatedOn,
            Items = o.Items.Select(item => MapOrderItem(item)).ToList(),
            Payments = o.Payments.Select(p => new OrderPaymentDetailViewModel
            {
                Id            = p.Id,
                Amount        = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Note          = p.Note,
                PaidAt        = p.PaidAt
            }).ToList(),
            Alterations = o.Alterations.Select(a => new OrderAlterationViewModel
            {
                Id              = a.Id,
                OrderItemId     = a.OrderItemId,
                Description     = a.Description,
                AdditionalCharge= a.AdditionalCharge,
                DeliveryDate    = a.DeliveryDate,
                CreatedOn       = a.CreatedOn
            }).ToList()
        };
    }

    private static OrderItemDetailViewModel MapOrderItem(OrderItem item)
    {
        // Deserialize measurement snapshot
        Dictionary<string, decimal> snapshot = new();
        if (!string.IsNullOrEmpty(item.MeasurementSnapshot))
        {
            try { snapshot = JsonSerializer.Deserialize<Dictionary<string, decimal>>(item.MeasurementSnapshot) ?? new(); }
            catch { /* ignore */ }
        }

        // Build stage progress from logs
        var logsByStage = item.StageLogs
            .GroupBy(l => l.Stage)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.CreatedOn).ToList());

        var stages = item.StageAssignments.Select(a => new StageProgressViewModel
        {
            Stage        = a.Stage,
            KarigarName  = a.AssignedKarigar?.Name,
            Status       = logsByStage.ContainsKey(a.Stage)
                            ? (logsByStage[a.Stage].Any(l => l.CompletedAt.HasValue) ? "Done" : "InProgress")
                            : "Pending",
            StartedAt    = logsByStage.ContainsKey(a.Stage) ? logsByStage[a.Stage].First().CreatedOn : null,
            CompletedAt  = logsByStage.ContainsKey(a.Stage) ? logsByStage[a.Stage].FirstOrDefault(l => l.CompletedAt.HasValue)?.CompletedAt : null
        }).ToList();

        return new OrderItemDetailViewModel
        {
            Id                  = item.Id,
            GarmentType         = item.GarmentType,
            FabricDescription   = item.FabricDescription,
            FabricColor         = item.FabricColor,
            StyleNotes          = item.StyleNotes,
            Price               = item.Price,
            Qty                 = item.Qty,
            Status              = item.Status,
            MeasurementSnapshot = snapshot,
            StageProgress       = stages
        };
    }

    public async Task<PagedResult<OrderListViewModel>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(q)
                || (o.Customer != null && o.Customer.Name.ToLower().Contains(q)));
        }
        if (filter.Status.HasValue)
            query = query.Where(o => o.Status == filter.Status.Value);
        if (filter.DateFrom.HasValue)
            query = query.Where(o => o.CreatedOn >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(o => o.CreatedOn <= filter.DateTo.Value);
        if (filter.DueToday == true)
            query = query.Where(o => o.DeliveryDate.Date == DateTime.Today);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedOn)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new OrderListViewModel
            {
                Id            = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                ItemCount     = o.Items.Count,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                Status        = o.Status,
                DeliveryDate  = o.DeliveryDate,
                CreatedOn     = o.CreatedOn
            })
            .ToListAsync(ct);

        return PagedResult<OrderListViewModel>.From(items, total, filter.Page, filter.PageSize);
    }

    public async Task<List<OrderListViewModel>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => new OrderListViewModel
            {
                Id            = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                ItemCount     = o.Items.Count,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                Status        = o.Status,
                DeliveryDate  = o.DeliveryDate,
                CreatedOn     = o.CreatedOn
            })
            .ToListAsync(ct);

    public async Task<List<OrderListViewModel>> GetDueTodayAsync(CancellationToken ct = default)
        => await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.DeliveryDate.Date == DateTime.Today
                && o.Status != OrderStatus.Delivered
                && o.Status != OrderStatus.Cancelled)
            .Select(o => new OrderListViewModel
            {
                Id            = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                ItemCount     = o.Items.Count,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                Status        = o.Status,
                DeliveryDate  = o.DeliveryDate,
                CreatedOn     = o.CreatedOn
            })
            .ToListAsync(ct);

    public async Task<List<OrderListViewModel>> GetOverdueAsync(CancellationToken ct = default)
        => await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.DeliveryDate.Date < DateTime.Today
                && o.Status != OrderStatus.Delivered
                && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.DeliveryDate)
            .Select(o => new OrderListViewModel
            {
                Id            = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                ItemCount     = o.Items.Count,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                Status        = o.Status,
                DeliveryDate  = o.DeliveryDate,
                CreatedOn     = o.CreatedOn
            })
            .ToListAsync(ct);

    public async Task<Order> CreateAsync(Order order, CancellationToken ct = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        _db.Orders.Update(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, OrderStatus status, CancellationToken ct = default)
    {
        var o = await _db.Orders.FindAsync(new object[] { id }, ct);
        if (o == null) return;
        o.Status = status;
        o.UpdatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<OrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default)
        => await _db.OrderItems
            .Include(i => i.StageLogs)
            .Include(i => i.StageAssignments)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
