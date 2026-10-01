using System.Text.Json;
using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Orders;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Orders;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly IOrderRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;
    private readonly IKarigarLedgerService _ledger;
    private readonly INotificationService _notifications;
    private readonly IPlanLimitService _limits;
    private readonly IShopSettingsCache _shops;
    private readonly OrderCore _core;

    public OrderService(
        ApplicationDbContext db,
        IOrderRepository repo,
        ICurrentUserService currentUser,
        IBusinessCalendar calendar,
        IKarigarLedgerService ledger,
        INotificationService notifications,
        IPlanLimitService limits,
        IShopSettingsCache shops,
        OrderCore core)
    {
        _db = db;
        _repo = repo;
        _currentUser = currentUser;
        _calendar = calendar;
        _ledger = ledger;
        _notifications = notifications;
        _limits = limits;
        _shops = shops;
        _core = core;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<OrderListViewModel>>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default)
        => ApiResponse<PagedResult<OrderListViewModel>>.Ok(await _repo.SearchAsync(filter, await _calendar.TodayAsync(ct), ct));

    public async Task<ApiResponse<OrderDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<OrderDetailViewModel>.NotFound("Order not found.")
            : ApiResponse<OrderDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<List<OrderStatusHistoryViewModel>>> GetHistoryAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _db.Orders.AnyAsync(o => o.Id == id, ct))
            return ApiResponse<List<OrderStatusHistoryViewModel>>.NotFound("Order not found.");
        return ApiResponse<List<OrderStatusHistoryViewModel>>.Ok(await _repo.GetHistoryAsync(id, ct));
    }

    public async Task<ApiResponse<List<OrderListViewModel>>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetByCustomerAsync(customerId, 200, ct));

    public async Task<ApiResponse<List<OrderListViewModel>>> GetDueTodayAsync(CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetDueOnAsync(await _calendar.TodayAsync(ct), 500, ct));

    public async Task<ApiResponse<List<OrderListViewModel>>> GetOverdueAsync(CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetOverdueAsync(await _calendar.TodayAsync(ct), 500, ct));

    public async Task<ApiResponse<DeliveryBoardViewModel>> GetBoardAsync(int perBucket, CancellationToken ct = default)
    {
        var tz = await _calendar.GetTimeZoneAsync(ct);
        var today = ShopCalendar.Today(_calendar.UtcNow, tz);
        var (start, end) = ShopCalendar.LocalDayBounds(today, tz);
        return ApiResponse<DeliveryBoardViewModel>.Ok(await _repo.GetBoardAsync(today, start, end, Math.Clamp(perBucket, 1, 100), ct));
    }

    public async Task<ApiResponse<OrderReceiptViewModel>> GetReceiptAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _repo.GetDetailAsync(id, ct);
        if (o == null) return ApiResponse<OrderReceiptViewModel>.NotFound("Order not found.");
        var shop = await _shops.GetAsync(_currentUser.ShopId, ct);

        return ApiResponse<OrderReceiptViewModel>.Ok(new OrderReceiptViewModel
        {
            ShopName       = shop?.Name ?? string.Empty,
            ShopPhone      = shop?.Phone,
            CurrencySymbol = shop?.CurrencySymbol ?? "₨",
            OrderNumber    = o.OrderNumber,
            OrderDate      = o.CreatedOn,
            DeliveryDate   = o.DeliveryDate,
            CustomerName   = o.CustomerName,
            CustomerPhone  = o.CustomerPhone,
            Lines = o.Items.Select(i => new OrderReceiptLineViewModel
            {
                Description = i.FabricCharge > 0 ? $"{i.GarmentType} (incl. fabric {i.FabricCharge:N0})" : i.GarmentType.ToString(),
                Qty         = i.Qty,
                UnitPrice   = i.Price,
                LineTotal   = OrderMath.LineTotal(i.Price, i.Qty, i.FabricCharge)
            }).ToList(),
            SubTotal    = o.SubTotal,
            Discount    = o.Discount,
            Alterations = o.Alterations.Where(a => a.Status != AlterationStatus.Cancelled).Sum(a => a.AdditionalCharge),
            GrandTotal  = o.GrandTotal,
            AmountPaid  = o.AmountPaid,
            BalanceDue  = o.BalanceDue,
            Payments    = o.Payments,
            Status      = o.Status
        });
    }

    // ── Create ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> CreateAsync(CreateOrderViewModel vm, CancellationToken ct = default)
    {
        if (vm.Items.Count == 0) return ApiResponse<OrderDetailViewModel>.Fail("An order needs at least one item.");

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == vm.CustomerId, ct);
        if (customer == null) return ApiResponse<OrderDetailViewModel>.NotFound("Customer not found.");

        var limit = await _limits.CheckAsync(PlanLimit.OrdersPerMonth, ct);
        if (limit != null) return ApiResponse<OrderDetailViewModel>.PaymentRequired(limit);

        // Karigars referenced by the stage plan must exist in this tenant.
        var karigarIds = vm.Items.SelectMany(i => i.StageAssignments ?? new())
            .Where(a => a.AssignedKarigarId.HasValue).Select(a => a.AssignedKarigarId!.Value).Distinct().ToList();
        if (karigarIds.Count > 0)
        {
            var found = await _db.AppUsers.CountAsync(u => karigarIds.Contains(u.Id) && u.ActiveStatus == ActiveStatus.Active, ct);
            if (found != karigarIds.Count) return ApiResponse<OrderDetailViewModel>.NotFound("One or more assigned karigars were not found.");
        }

        // Measurement profiles must belong to this customer.
        var profileIds = vm.Items.Where(i => i.MeasurementProfileId.HasValue).Select(i => i.MeasurementProfileId!.Value).Distinct().ToList();
        var profiles = await _db.MeasurementProfiles.AsNoTracking().Include(p => p.Template)
            .Where(p => profileIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (profiles.Count != profileIds.Count) return ApiResponse<OrderDetailViewModel>.NotFound("Measurement profile not found.");
        if (profiles.Values.Any(p => p.CustomerId != vm.CustomerId))
            return ApiResponse<OrderDetailViewModel>.Fail("A measurement profile belongs to a different customer.");

        var attributeError = await ValidateAttributesAsync(vm.Items, ct);
        if (attributeError != null) return ApiResponse<OrderDetailViewModel>.Fail(attributeError);

        var subTotal = vm.Items.Sum(i => OrderMath.LineTotal(i.Price, i.Qty, i.FabricCharge));
        var discountError = OrderMath.ValidateDiscount(subTotal, vm.Discount);
        if (discountError != null) return ApiResponse<OrderDetailViewModel>.Fail(discountError);
        var grandTotal = OrderMath.GrandTotal(subTotal, vm.Discount, 0);
        if (vm.AdvancePayment < 0) return ApiResponse<OrderDetailViewModel>.Fail("Advance payment cannot be negative.");
        if (vm.AdvancePayment > grandTotal)
            return ApiResponse<OrderDetailViewModel>.Fail($"Advance payment ({vm.AdvancePayment:N2}) cannot exceed the order total ({grandTotal:N2}).");

        var now = _calendar.UtcNow;
        var order = new Order
        {
            Id                  = Guid.NewGuid(),
            TenantId            = _currentUser.TenantId,
            BranchId            = _currentUser.ShopId,
            CustomerId          = vm.CustomerId,
            CreatedByUserId     = _currentUser.UserId,
            // Stamped explicitly (rather than left to the DbContext's own clock read on save) so
            // it never lands a moment after the advance payment's PaidAt, which would otherwise
            // show the payment ahead of the order on the customer's ledger.
            CreatedOn           = now,
            UpdatedOn           = now,
            OrderNumber         = GenerateOrderNumber(now),
            DeliveryDate        = await _calendar.ToCalendarDateAsync(vm.DeliveryDate, ct),
            Notes               = vm.Notes,
            SpecialInstructions = vm.SpecialInstructions,
            Priority            = vm.Priority,
            TrialRequired       = vm.TrialRequired,
            TrialDate           = vm.TrialDate.HasValue ? await _calendar.ToCalendarDateAsync(vm.TrialDate.Value, ct) : null,
            Discount            = vm.Discount,
            SubTotal            = subTotal,
            Status              = OrderStatus.Pending,
            ActiveStatus        = ActiveStatus.Active
        };

        foreach (var itemVm in vm.Items)
        {
            var item = new OrderItem
            {
                Id                = Guid.NewGuid(),
                OrderId           = order.Id,
                GarmentType       = itemVm.GarmentType,
                FabricDescription = itemVm.FabricDescription,
                FabricColor       = itemVm.FabricColor,
                FabricSource      = itemVm.FabricSource,
                FabricCharge      = itemVm.FabricCharge,
                StyleNotes        = itemVm.StyleNotes,
                AttributesJson    = itemVm.Attributes is { Count: > 0 } ? JsonSerializer.Serialize(itemVm.Attributes) : null,
                Price             = itemVm.Price,
                Qty               = itemVm.Qty,
                Status            = OrderItemStatus.Pending,
                ActiveStatus      = ActiveStatus.Active
            };

            if (itemVm.MeasurementProfileId.HasValue)
            {
                var profile = profiles[itemVm.MeasurementProfileId.Value];
                item.MeasurementProfileId = profile.Id;
                item.MeasurementProfileVersion = profile.CurrentVersion;
                var snapshot = await BuildSnapshotAsync(profile.TemplateId, profile.Template?.Name,
                    Deserialize<decimal>(profile.FieldValuesJson), Deserialize<string>(profile.TextValuesJson), ct);
                snapshot.ProfileId = profile.Id;
                snapshot.ProfileVersion = profile.CurrentVersion;
                item.MeasurementSnapshot = snapshot.Serialize();
            }
            else if (itemVm.InlineMeasurements is { Count: > 0 } || itemVm.InlineTextMeasurements is { Count: > 0 })
            {
                string? templateName = null;
                if (itemVm.TemplateId.HasValue)
                    templateName = await _db.MeasurementTemplates.Where(t => t.Id == itemVm.TemplateId).Select(t => t.Name).FirstOrDefaultAsync(ct);
                item.MeasurementSnapshot = (await BuildSnapshotAsync(itemVm.TemplateId, templateName,
                    itemVm.InlineMeasurements ?? new(), itemVm.InlineTextMeasurements ?? new(), ct)).Serialize();
            }

            foreach (var asgn in itemVm.StageAssignments ?? new())
            {
                if (item.StageAssignments.Any(a => a.Stage == asgn.Stage))
                    return ApiResponse<OrderDetailViewModel>.Fail($"Stage {asgn.Stage} is planned twice for the same item.");
                item.StageAssignments.Add(new OrderItemStageAssignment
                {
                    Id                = Guid.NewGuid(),
                    OrderItemId       = item.Id,
                    Stage             = asgn.Stage,
                    AssignedKarigarId = asgn.AssignedKarigarId,
                    StagePrice        = asgn.StagePrice
                                        ?? await _ledger.ResolveStagePriceAsync(asgn.AssignedKarigarId, item.GarmentType, asgn.Stage, item.Qty, ct),
                    EstimatedDays     = asgn.EstimatedDays
                });
            }
            order.Items.Add(item);
        }

        if (vm.AdvancePayment > 0)
        {
            order.Payments.Add(new OrderPayment
            {
                Id            = Guid.NewGuid(),
                OrderId       = order.Id,
                Amount        = vm.AdvancePayment,
                PaymentMethod = vm.PaymentMethod,
                Note          = "Advance payment",
                PaidAt        = now
            });
        }
        OrderCore.RecalculateMoney(order);

        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId    = order.Id,
            ChangedBy  = _currentUser.UserId,
            FromStatus = OrderStatus.Pending,
            ToStatus   = OrderStatus.Pending,
            ChangedOn  = now,
            Notes      = "Order created"
        });

        _db.Orders.Add(order);
        order.Customer = await _db.Customers.FirstAsync(c => c.Id == vm.CustomerId, ct);
        await _notifications.EnqueueOrderEventAsync(order, NotificationEvent.OrderConfirmed, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _core.AdjustCustomerStatsAsync(vm.CustomerId, 1, order.GrandTotal, OrderMath.PointsPerOrder, ct);
        await tx.CommitAsync(ct);

        return ApiResponse<OrderDetailViewModel>.Ok((await _repo.GetDetailAsync(order.Id, ct))!, "Order created.");
    }

    // ── Update ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> UpdateAsync(UpdateOrderViewModel vm, CancellationToken ct = default)
    {
        var order = await _core.LoadForMoneyAsync(vm.Id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");
        if (OrderStateMachine.IsTerminal(order.Status))
            return ApiResponse<OrderDetailViewModel>.Fail("Cannot edit a delivered or cancelled order.");

        if (vm.Discount.HasValue)
        {
            var error = OrderMath.ValidateDiscount(order.SubTotal, vm.Discount.Value);
            if (error != null) return ApiResponse<OrderDetailViewModel>.Fail(error);
        }

        var previousTotal = order.GrandTotal;
        if (vm.DeliveryDate.HasValue) order.DeliveryDate = await _calendar.ToCalendarDateAsync(vm.DeliveryDate.Value, ct);
        if (vm.Notes != null) order.Notes = vm.Notes;
        if (vm.SpecialInstructions != null) order.SpecialInstructions = vm.SpecialInstructions;
        if (vm.Priority.HasValue) order.Priority = vm.Priority.Value;
        if (vm.TrialRequired.HasValue) order.TrialRequired = vm.TrialRequired.Value;
        if (vm.TrialDate.HasValue) order.TrialDate = await _calendar.ToCalendarDateAsync(vm.TrialDate.Value, ct);
        if (vm.Discount.HasValue) order.Discount = vm.Discount.Value;

        // Alteration charges stay in the total (they were dropped here before).
        OrderCore.RecalculateMoney(order);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (order.GrandTotal != previousTotal)
            await _core.AdjustCustomerStatsAsync(order.CustomerId, 0, order.GrandTotal - previousTotal, 0, ct);
        await tx.CommitAsync(ct);

        return ApiResponse<OrderDetailViewModel>.Ok((await _repo.GetDetailAsync(order.Id, ct))!, "Order updated.");
    }

    // ── Reorder ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> ReorderAsync(Guid id, ReorderViewModel vm, CancellationToken ct = default)
    {
        var original = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.StageAssignments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (original == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");

        var create = new CreateOrderViewModel
        {
            CustomerId    = original.CustomerId,
            DeliveryDate  = vm.DeliveryDate,
            Priority      = original.Priority,
            TrialRequired = original.TrialRequired,
            Notes         = $"Repeat of {original.OrderNumber}",
            SpecialInstructions = original.SpecialInstructions
        };

        foreach (var item in original.Items.Where(i => !i.IsDeleted && i.Status != OrderItemStatus.Cancelled))
        {
            var snapshot = MeasurementSnapshot.Parse(item.MeasurementSnapshot);
            var useProfile = vm.UseLatestMeasurements && item.MeasurementProfileId.HasValue
                && await _db.MeasurementProfiles.AnyAsync(p => p.Id == item.MeasurementProfileId, ct);

            create.Items.Add(new CreateOrderItemViewModel
            {
                GarmentType            = item.GarmentType,
                MeasurementProfileId   = useProfile ? item.MeasurementProfileId : null,
                TemplateId             = useProfile ? null : snapshot.TemplateId,
                InlineMeasurements     = useProfile ? null : snapshot.Values.Where(v => v.Number.HasValue).ToDictionary(v => v.FieldId, v => v.Number!.Value),
                InlineTextMeasurements = useProfile ? null : snapshot.Values.Where(v => v.Text != null).ToDictionary(v => v.FieldId, v => v.Text!),
                FabricDescription      = item.FabricDescription,
                FabricColor            = item.FabricColor,
                FabricSource           = item.FabricSource,
                FabricCharge           = item.FabricCharge,
                StyleNotes             = item.StyleNotes,
                Attributes             = string.IsNullOrWhiteSpace(item.AttributesJson)
                                             ? null
                                             : JsonSerializer.Deserialize<Dictionary<string, string>>(item.AttributesJson),
                Price                  = item.Price,
                Qty                    = item.Qty,
                StageAssignments       = item.StageAssignments.Select(a => new CreateOrderItemStageAssignmentViewModel
                {
                    Stage = a.Stage, AssignedKarigarId = a.AssignedKarigarId, EstimatedDays = a.EstimatedDays
                }).ToList()
            });
        }

        return await CreateAsync(create, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<MeasurementSnapshot> BuildSnapshotAsync(Guid? templateId, string? templateName,
        Dictionary<Guid, decimal> numbers, Dictionary<Guid, string> texts, CancellationToken ct)
    {
        var ids = numbers.Keys.Concat(texts.Keys).Distinct().ToList();
        var fields = await _db.MeasurementFields.AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);

        var snapshot = new MeasurementSnapshot { TemplateId = templateId, TemplateName = templateName };
        foreach (var id in ids)
        {
            fields.TryGetValue(id, out var field);
            snapshot.Values.Add(new MeasurementSnapshot.Value
            {
                FieldId = id,
                Name    = field?.Name ?? id.ToString(),
                Unit    = field?.Unit,
                Number  = numbers.TryGetValue(id, out var n) ? n : null,
                Text    = texts.TryGetValue(id, out var t) ? t : null
            });
        }
        return snapshot;
    }

    private async Task<string?> ValidateAttributesAsync(List<CreateOrderItemViewModel> items, CancellationToken ct)
    {
        var catalogue = await _db.GarmentAttributes.AsNoTracking().ToListAsync(ct);
        if (catalogue.Count == 0) return null;

        foreach (var item in items)
        {
            var given = item.Attributes ?? new Dictionary<string, string>();
            foreach (var (name, value) in given)
            {
                var def = catalogue.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
                    && (a.GarmentType == null || a.GarmentType == item.GarmentType));
                if (def == null) continue;   // free-form attribute the shop has not catalogued

                var options = string.IsNullOrWhiteSpace(def.OptionsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(def.OptionsJson) ?? new();
                if (options.Count > 0 && !options.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))
                    return $"'{value}' is not a valid option for {def.Name}. Choose one of: {string.Join(", ", options)}.";
            }

            var missing = catalogue.Where(a => a.IsRequired && (a.GarmentType == null || a.GarmentType == item.GarmentType)
                    && !given.Keys.Any(k => string.Equals(k, a.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(a => a.Name).ToList();
            if (missing.Count > 0) return $"{item.GarmentType} needs: {string.Join(", ", missing)}.";
        }
        return null;
    }

    private static Dictionary<Guid, T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<Guid, T>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string GenerateOrderNumber(DateTime now)
        => $"ORD-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
