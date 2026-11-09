using System.Linq.Expressions;
using System.Text.Json;
using Application.Common;
using Application.Interfaces.Repositories;
using Application.Orders;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(ApplicationDbContext db) : base(db) { }

    private static readonly OrderStatus[] Closed = { OrderStatus.Delivered, OrderStatus.Cancelled };

    /// <summary>
    /// Statuses that are never "late". A ready order past its date is waiting for the customer to
    /// collect it; the shop has done its part, so it shows under Ready, not Late.
    /// </summary>
    private static readonly OrderStatus[] NotLate =
        { OrderStatus.Delivered, OrderStatus.Cancelled, OrderStatus.ReadyToDeliver, OrderStatus.PartiallyDelivered };

    // ── Detail ───────────────────────────────────────────────────────────────

    public async Task<OrderDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.StageLogs).ThenInclude(l => l.Karigar)
            .Include(x => x.Items).ThenInclude(i => i.StageAssignments).ThenInclude(a => a.AssignedKarigar)
            .Include(x => x.Payments)
            .Include(x => x.Alterations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (o == null) return null;

        var karigarIds = o.Alterations.Where(a => a.AssignedKarigarId.HasValue).Select(a => a.AssignedKarigarId!.Value).ToList();
        var names = karigarIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.AppUsers.AsNoTracking().Where(u => karigarIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return new OrderDetailViewModel
        {
            Id                  = o.Id,
            OrderNumber         = o.OrderNumber,
            CustomerId          = o.CustomerId,
            CustomerName        = o.Customer?.Name ?? string.Empty,
            CustomerPhone       = o.Customer?.Phone ?? string.Empty,
            DeliveryDate        = o.DeliveryDate,
            ReadyDate           = o.ReadyDate,
            ActualDeliveryDate  = o.ActualDeliveryDate,
            Notes               = o.Notes,
            SpecialInstructions = o.SpecialInstructions,
            Priority            = o.Priority,
            SubTotal            = o.SubTotal,
            Discount            = o.Discount,
            GrandTotal          = o.GrandTotal,
            AmountPaid          = o.AmountPaid,
            BalanceDue          = o.BalanceDue,
            Status              = o.Status,
            PaymentStatus       = o.PaymentStatus,
            AllowedNextStatuses = OrderStateMachine.AllowedNext(o.Status).ToList(),
            TrialRequired       = o.TrialRequired,
            TrialDate           = o.TrialDate,
            TrialNotes          = o.TrialNotes,
            CancellationReason  = o.CancellationReason,
            CancellationStage   = o.CancellationStage,
            RefundAmount        = o.RefundAmount,
            RefundStatus        = o.RefundStatus,
            CreatedOn           = o.CreatedOn,
            Items               = o.Items.OrderBy(i => i.CreatedOn).Select(MapOrderItem).ToList(),
            Payments            = o.Payments.OrderBy(p => p.PaidAt).Select(p => new OrderPaymentDetailViewModel
            {
                Id            = p.Id,
                Amount        = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Note          = p.Note,
                IsRefund      = p.IsRefund,
                PaidAt        = p.PaidAt
            }).ToList(),
            Alterations = o.Alterations.OrderBy(a => a.CreatedOn).Select(a => MapAlteration(a, names)).ToList(),
            History     = await GetHistoryAsync(o.Id, ct)
        };
    }

    public async Task<List<OrderStatusHistoryViewModel>> GetHistoryAsync(Guid orderId, CancellationToken ct = default)
    {
        var rows = await _db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedOn)
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.ChangedBy).Distinct().ToList();
        var names = await _db.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.TenantId == _db.CurrentTenantId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return rows.Select(h => new OrderStatusHistoryViewModel
        {
            FromStatus       = h.FromStatus,
            ToStatus         = h.ToStatus,
            ChangedBy        = h.ChangedBy,
            ChangedByName    = names.GetValueOrDefault(h.ChangedBy),
            ChangedOn        = h.ChangedOn,
            IsReversal       = h.IsReversal,
            IsManualOverride = h.IsManualOverride,
            OrderItemId      = h.OrderItemId,
            Notes            = h.Notes
        }).ToList();
    }

    public async Task<List<OrderAlterationViewModel>> GetAlterationsAsync(Guid orderId, CancellationToken ct = default)
    {
        var rows = await _db.OrderAlterations.AsNoTracking().Where(a => a.OrderId == orderId).OrderBy(a => a.CreatedOn).ToListAsync(ct);
        var ids = rows.Where(a => a.AssignedKarigarId.HasValue).Select(a => a.AssignedKarigarId!.Value).ToList();
        var names = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return rows.Select(a => MapAlteration(a, names)).ToList();
    }

    // ── Search ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<OrderListViewModel>> SearchAsync(OrderSearchViewModel filter, DateTime today, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);
        var query = _db.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = Like.Contains(filter.Query);
            query = query.Where(o => EF.Functions.ILike(o.OrderNumber, pattern, "\\")
                || EF.Functions.ILike(o.Customer.Name, pattern, "\\")
                || EF.Functions.ILike(o.Customer.Phone, pattern, "\\"));
        }
        if (filter.Status.HasValue)        query = query.Where(o => o.Status == filter.Status.Value);
        if (filter.PaymentStatus.HasValue) query = query.Where(o => o.PaymentStatus == filter.PaymentStatus.Value);
        if (filter.Priority.HasValue)      query = query.Where(o => o.Priority == filter.Priority.Value);
        if (filter.CustomerId.HasValue)    query = query.Where(o => o.CustomerId == filter.CustomerId.Value);
        if (filter.GarmentType.HasValue)   query = query.Where(o => o.Items.Any(i => i.GarmentType == filter.GarmentType.Value));
        if (filter.KarigarId.HasValue)
            query = query.Where(o => o.Items.Any(i => i.StageAssignments.Any(a => a.AssignedKarigarId == filter.KarigarId.Value)));
        if (filter.DateFrom.HasValue)      query = query.Where(o => o.CreatedOn >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)        query = query.Where(o => o.CreatedOn <= filter.DateTo.Value);
        if (filter.DeliveryFrom.HasValue)  query = query.Where(o => o.DeliveryDate >= filter.DeliveryFrom.Value);
        if (filter.DeliveryTo.HasValue)    query = query.Where(o => o.DeliveryDate <= filter.DeliveryTo.Value);
        if (filter.HasBalance == true)     query = query.Where(o => o.BalanceDue > 0 && o.Status != OrderStatus.Cancelled);
        if (filter.DueToday == true)
        {
            var tomorrow = today.AddDays(1);
            query = query.Where(o => o.DeliveryDate >= today && o.DeliveryDate < tomorrow);
        }
        if (filter.Overdue == true)
            query = query.Where(o => o.DeliveryDate < today && !NotLate.Contains(o.Status));

        var total = await query.CountAsync(ct);
        var items = await ApplySort(query, filter.SortBy, filter.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ListProjection)
            .ToListAsync(ct);

        return PagedResult<OrderListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<List<OrderListViewModel>> GetByCustomerAsync(Guid customerId, int limit, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedOn)
            .Take(limit)
            .Select(ListProjection)
            .ToListAsync(ct);

    // Half-open range on the calendar date so the (TenantId, BranchId, DeliveryDate) index is used.
    public async Task<List<OrderListViewModel>> GetDueOnAsync(DateTime calendarDate, int limit, CancellationToken ct = default)
    {
        var next = calendarDate.AddDays(1);
        return await _db.Orders.AsNoTracking()
            .Where(o => o.DeliveryDate >= calendarDate && o.DeliveryDate < next && !Closed.Contains(o.Status))
            .OrderBy(o => o.Priority == OrderPriority.Normal).ThenBy(o => o.CreatedOn)
            .Take(limit)
            .Select(ListProjection)
            .ToListAsync(ct);
    }

    public async Task<List<OrderListViewModel>> GetOverdueAsync(DateTime today, int limit, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking()
            .Where(o => o.DeliveryDate < today && !NotLate.Contains(o.Status))
            .OrderBy(o => o.DeliveryDate)
            .Take(limit)
            .Select(ListProjection)
            .ToListAsync(ct);

    public async Task<DeliveryBoardViewModel> GetBoardAsync(DateTime today, DateTime todayStartUtc, DateTime todayEndUtc, int perBucket, CancellationToken ct = default)
    {
        var tomorrow = today.AddDays(1);
        var dayAfter = today.AddDays(2);
        var open = _db.Orders.AsNoTracking().Where(o => !Closed.Contains(o.Status));

        // One grouped query for all bucket counts.
        var counts = await open
            .GroupBy(_ => 1)
            .Select(g => new
            {
                DueToday    = g.Count(o => o.DeliveryDate >= today && o.DeliveryDate < tomorrow),
                DueTomorrow = g.Count(o => o.DeliveryDate >= tomorrow && o.DeliveryDate < dayAfter),
                Overdue     = g.Count(o => o.DeliveryDate < today && o.Status != OrderStatus.ReadyToDeliver && o.Status != OrderStatus.PartiallyDelivered),
                Ready       = g.Count(o => o.Status == OrderStatus.ReadyToDeliver || o.Status == OrderStatus.PartiallyDelivered)
            })
            .FirstOrDefaultAsync(ct);

        var deliveredToday = _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered && o.ActualDeliveryDate >= todayStartUtc && o.ActualDeliveryDate < todayEndUtc);

        return new DeliveryBoardViewModel
        {
            Today               = today,
            DueTodayCount       = counts?.DueToday ?? 0,
            DueTomorrowCount    = counts?.DueTomorrow ?? 0,
            OverdueCount        = counts?.Overdue ?? 0,
            ReadyForPickupCount = counts?.Ready ?? 0,
            DeliveredTodayCount = await deliveredToday.CountAsync(ct),
            DueToday    = await GetDueOnAsync(today, perBucket, ct),
            DueTomorrow = await GetDueOnAsync(tomorrow, perBucket, ct),
            Overdue     = await GetOverdueAsync(today, perBucket, ct),
            ReadyForPickup = await open
                .Where(o => o.Status == OrderStatus.ReadyToDeliver || o.Status == OrderStatus.PartiallyDelivered)
                .OrderBy(o => o.ReadyDate).Take(perBucket).Select(ListProjection).ToListAsync(ct),
            DeliveredToday = await deliveredToday
                .OrderByDescending(o => o.ActualDeliveryDate).Take(perBucket).Select(ListProjection).ToListAsync(ct)
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static readonly Expression<Func<Order, OrderListViewModel>> ListProjection = o => new OrderListViewModel
    {
        Id            = o.Id,
        OrderNumber   = o.OrderNumber,
        CustomerId    = o.CustomerId,
        CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
        CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
        ItemCount     = o.Items.Count(i => !i.IsDeleted),
        GrandTotal    = o.GrandTotal,
        AmountPaid    = o.AmountPaid,
        BalanceDue    = o.BalanceDue,
        Status        = o.Status,
        PaymentStatus = o.PaymentStatus,
        Priority      = o.Priority,
        DeliveryDate  = o.DeliveryDate,
        ReadyDate     = o.ReadyDate,
        CreatedOn     = o.CreatedOn
    };

    private static IQueryable<Order> ApplySort(IQueryable<Order> q, string? sortBy, string? direction)
    {
        var asc = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "deliverydate" => asc ? q.OrderBy(o => o.DeliveryDate) : q.OrderByDescending(o => o.DeliveryDate),
            "grandtotal"   => asc ? q.OrderBy(o => o.GrandTotal) : q.OrderByDescending(o => o.GrandTotal),
            "balancedue"   => asc ? q.OrderBy(o => o.BalanceDue) : q.OrderByDescending(o => o.BalanceDue),
            "ordernumber"  => asc ? q.OrderBy(o => o.OrderNumber) : q.OrderByDescending(o => o.OrderNumber),
            _              => asc ? q.OrderBy(o => o.CreatedOn) : q.OrderByDescending(o => o.CreatedOn)
        };
    }

    private static OrderAlterationViewModel MapAlteration(OrderAlteration a, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id                  = a.Id,
        OrderId             = a.OrderId,
        OrderItemId         = a.OrderItemId,
        Description         = a.Description,
        RequiredChange      = a.RequiredChange,
        Status              = a.Status,
        AdditionalCharge    = a.AdditionalCharge,
        DeliveryDate        = a.DeliveryDate,
        AssignedKarigarId   = a.AssignedKarigarId,
        AssignedKarigarName = a.AssignedKarigarId.HasValue ? names.GetValueOrDefault(a.AssignedKarigarId.Value) : null,
        CompletedOn         = a.CompletedOn,
        DeliveredOn         = a.DeliveredOn,
        Notes               = a.Notes,
        CreatedOn           = a.CreatedOn
    };

    private static OrderItemDetailViewModel MapOrderItem(OrderItem item)
    {
        var snapshot = MeasurementSnapshot.Parse(item.MeasurementSnapshot);

        Dictionary<string, string> attributes = new();
        if (!string.IsNullOrWhiteSpace(item.AttributesJson))
        {
            try { attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(item.AttributesJson) ?? new(); }
            catch (JsonException) { /* corrupt attribute JSON is shown as empty rather than failing the order */ }
        }

        var logsByStage = item.StageLogs
            .GroupBy(l => l.Stage)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.CreatedOn).ToList());

        var stages = item.StageAssignments.OrderBy(a => a.Stage).Select(a =>
        {
            logsByStage.TryGetValue(a.Stage, out var logs);
            logs ??= new List<OrderItemStageLog>();
            var latest = logs.LastOrDefault();
            var status = latest == null ? "Pending"
                : latest.Status == StageLogStatus.ReworkRequired ? "Rework"
                : latest.CompletedAt.HasValue ? "Done"
                : "InProgress";
            return new StageProgressViewModel
            {
                Stage         = a.Stage,
                KarigarId     = a.AssignedKarigarId,
                KarigarName   = a.AssignedKarigar?.Name,
                Status        = status,
                StagePrice    = a.StagePrice,
                EstimatedDays = a.EstimatedDays,
                StartedAt     = logs.FirstOrDefault()?.CreatedOn,
                CompletedAt   = status == "Done" ? latest!.CompletedAt : null,
                ReworkCount   = logs.Count(l => l.Status == StageLogStatus.ReworkRequired)
            };
        }).ToList();

        return new OrderItemDetailViewModel
        {
            Id                        = item.Id,
            GarmentType               = item.GarmentType,
            FabricDescription         = item.FabricDescription,
            FabricColor               = item.FabricColor,
            FabricSource              = item.FabricSource,
            FabricCharge              = item.FabricCharge,
            StyleNotes                = item.StyleNotes,
            Attributes                = attributes,
            Price                     = item.Price,
            Qty                       = item.Qty,
            Status                    = item.Status,
            IsDelivered               = item.IsDelivered,
            DeliveredOn               = item.DeliveredOn,
            FittingNotes              = item.FittingNotes,
            MeasurementProfileId      = item.MeasurementProfileId,
            MeasurementProfileVersion = item.MeasurementProfileVersion,
            MeasurementTemplateName   = snapshot.TemplateName,
            MeasurementSnapshot       = snapshot.ToLegacyDictionary(),
            Measurements              = snapshot.Version >= 2
                ? snapshot.Values.Select(v => new MeasurementValueViewModel
                  {
                      FieldId = v.FieldId, Name = v.Name, Unit = v.Unit, Value = v.Number, Text = v.Text
                  }).ToList()
                : new List<MeasurementValueViewModel>(),
            StageProgress = stages
        };
    }
}
