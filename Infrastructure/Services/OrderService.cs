using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using System.Text.Json;

namespace Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IMeasurementRepository _measurementRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentService _attachmentService;
    private readonly IUnitOfWork _uow;

    public OrderService(
        IOrderRepository repo,
        ICustomerRepository customerRepo,
        IMeasurementRepository measurementRepo,
        ICurrentUserService currentUser,
        IAttachmentService attachmentService,
        IUnitOfWork uow)
    {
        _repo = repo;
        _customerRepo = customerRepo;
        _measurementRepo = measurementRepo;
        _currentUser = currentUser;
        _attachmentService = attachmentService;
        _uow = uow;
    }

    public async Task<ApiResponse<PagedResult<OrderListViewModel>>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default)
        => ApiResponse<PagedResult<OrderListViewModel>>.Ok(await _repo.SearchAsync(filter, ct));

    public async Task<ApiResponse<OrderDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<OrderDetailViewModel>.Fail("Order not found.")
            : ApiResponse<OrderDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<List<OrderListViewModel>>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetByCustomerAsync(customerId, ct));

    public async Task<ApiResponse<List<OrderListViewModel>>> GetDueTodayAsync(CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetDueTodayAsync(ct));

    public async Task<ApiResponse<List<OrderListViewModel>>> GetOverdueAsync(CancellationToken ct = default)
        => ApiResponse<List<OrderListViewModel>>.Ok(await _repo.GetOverdueAsync(ct));

    public async Task<ApiResponse<OrderDetailViewModel>> CreateAsync(CreateOrderViewModel vm, CancellationToken ct = default)
    {
        var customer = await _customerRepo.GetByIdAsync(vm.CustomerId, ct);
        if (customer == null) return ApiResponse<OrderDetailViewModel>.Fail("Customer not found.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var order = new Order
            {
                Id                  = Guid.NewGuid(),
                TenantId            = _currentUser.TenantId,
                BranchId            = _currentUser.ShopId,
                CustomerId          = vm.CustomerId,
                OrderNumber         = GenerateOrderNumber(),
                DeliveryDate        = vm.DeliveryDate,
                Notes               = vm.Notes,
                SpecialInstructions = vm.SpecialInstructions,
                Discount            = vm.Discount,
                Status              = OrderStatus.Pending,
                ActiveStatus        = ActiveStatus.Active,
                CreatedBy           = _currentUser.UserId,
                CreatedOn           = now,
                UpdatedBy           = _currentUser.UserId,
                UpdatedOn           = now
            };

            decimal subTotal = 0;
            foreach (var itemVm in vm.Items)
            {
                Dictionary<Guid, decimal> rawMeasurements = new();
                string snapshotJson = "{}";

                if (itemVm.MeasurementProfileId.HasValue)
                {
                    var profile = await _measurementRepo.GetProfileByIdAsync(itemVm.MeasurementProfileId.Value, ct);
                    if (profile != null && !string.IsNullOrEmpty(profile.FieldValuesJson))
                        rawMeasurements = JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(profile.FieldValuesJson) ?? new();
                }
                else if (itemVm.InlineMeasurements != null)
                {
                    rawMeasurements = itemVm.InlineMeasurements;
                }

                if (rawMeasurements.Any())
                    snapshotJson = JsonSerializer.Serialize(rawMeasurements);

                var item = new OrderItem
                {
                    Id                   = Guid.NewGuid(),
                    TenantId             = _currentUser.TenantId,
                    BranchId             = _currentUser.ShopId,
                    OrderId              = order.Id,
                    GarmentType          = itemVm.GarmentType,
                    MeasurementProfileId = itemVm.MeasurementProfileId,
                    MeasurementSnapshot  = snapshotJson,
                    FabricDescription    = itemVm.FabricDescription,
                    FabricColor          = itemVm.FabricColor,
                    StyleNotes           = itemVm.StyleNotes,
                    Price                = itemVm.Price,
                    Qty                  = itemVm.Qty,
                    Status               = OrderItemStatus.Pending,
                    ActiveStatus         = ActiveStatus.Active,
                    CreatedBy            = _currentUser.UserId,
                    CreatedOn            = now
                };

                if (itemVm.StageAssignments != null)
                {
                    foreach (var asgn in itemVm.StageAssignments)
                    {
                        item.StageAssignments.Add(new OrderItemStageAssignment
                        {
                            Id                = Guid.NewGuid(),
                            TenantId          = _currentUser.TenantId,
                            BranchId          = _currentUser.ShopId,
                            OrderItemId       = item.Id,
                            Stage             = asgn.Stage,
                            AssignedKarigarId = asgn.AssignedKarigarId,
                            StagePrice        = asgn.StagePrice,
                            EstimatedDays     = asgn.EstimatedDays,
                            CreatedBy         = _currentUser.UserId,
                            CreatedOn         = now
                        });
                    }
                }

                order.Items.Add(item);
                subTotal += item.Price * item.Qty;
            }

            order.SubTotal   = subTotal;
            order.GrandTotal = subTotal - vm.Discount;
            order.AmountPaid = vm.AdvancePayment;
            order.BalanceDue = order.GrandTotal - vm.AdvancePayment;

            if (vm.AdvancePayment > 0)
            {
                order.Payments.Add(new OrderPayment
                {
                    Id            = Guid.NewGuid(),
                    TenantId      = _currentUser.TenantId,
                    BranchId      = _currentUser.ShopId,
                    OrderId       = order.Id,
                    Amount        = vm.AdvancePayment,
                    PaymentMethod = vm.PaymentMethod,
                    Note          = "Advance payment",
                    PaidAt        = now,
                    CreatedBy     = _currentUser.UserId,
                    CreatedOn     = now
                });
            }

            await _repo.AddAsync(order, ct);

            // Update customer stats
            customer.TotalOrders++;
            customer.TotalSpend += order.GrandTotal;
            UpdateLoyaltyTier(customer);
            customer.UpdatedOn = now;
            await _customerRepo.UpdateAsync(customer, ct);

            await _uow.CommitAsync(ct);

            // Attachments after commit (file I/O outside transaction)
            if (vm.Attachments != null && vm.Attachments.Any())
            {
                foreach (var file in vm.Attachments)
                    await _attachmentService.AddAsync(order.Id, AttachmentType.Order, file, ct);
            }

            var detail = await _repo.GetDetailAsync(order.Id, ct);
            return ApiResponse<OrderDetailViewModel>.Ok(detail!);
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            return ApiResponse<OrderDetailViewModel>.Fail(ex.InnerException?.Message ?? ex.Message);
        }
    }

    public async Task<ApiResponse<OrderDetailViewModel>> UpdateAsync(Guid id, UpdateOrderViewModel vm, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.Fail("Order not found.");
        if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
            return ApiResponse<OrderDetailViewModel>.Fail("Cannot edit a delivered or cancelled order.");

        if (vm.DeliveryDate.HasValue) order.DeliveryDate = vm.DeliveryDate.Value;
        if (vm.Notes != null) order.Notes = vm.Notes;
        if (vm.SpecialInstructions != null) order.SpecialInstructions = vm.SpecialInstructions;
        if (vm.Discount.HasValue)
        {
            order.Discount   = vm.Discount.Value;
            order.GrandTotal = order.SubTotal - order.Discount;
            order.BalanceDue = order.GrandTotal - order.AmountPaid;
        }
        order.UpdatedBy = _currentUser.UserId;
        order.UpdatedOn = DateTime.UtcNow;

        await _repo.UpdateAsync(order, ct);
        var detail = await _repo.GetDetailAsync(order.Id, ct);
        return ApiResponse<OrderDetailViewModel>.Ok(detail!);
    }

    public async Task<ApiResponse<object>> UpdateStatusAsync(Guid id, OrderStatus status, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order == null) return ApiResponse<object>.Fail("Order not found.");
        await _repo.UpdateStatusAsync(id, status, ct);
        return ApiResponse<object>.Ok((object?)null, $"Order status updated to {status}.");
    }

    public async Task<ApiResponse<object>> RecordPaymentAsync(Guid orderId, RecordOrderPaymentViewModel vm, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order == null) return ApiResponse<object>.Fail("Order not found.");
        if (vm.Amount <= 0) return ApiResponse<object>.Fail("Payment amount must be greater than zero.");
        if (vm.Amount > order.BalanceDue) return ApiResponse<object>.Fail($"Amount exceeds balance due ({order.BalanceDue:N2}).");

        var now = DateTime.UtcNow;
        order.Payments.Add(new OrderPayment
        {
            Id            = Guid.NewGuid(),
            TenantId      = _currentUser.TenantId,
            BranchId      = _currentUser.ShopId,
            OrderId       = orderId,
            Amount        = vm.Amount,
            PaymentMethod = vm.PaymentMethod,
            Note          = vm.Note,
            PaidAt        = vm.PaidAt ?? now,
            CreatedBy     = _currentUser.UserId,
            CreatedOn     = now
        });

        order.AmountPaid += vm.Amount;
        order.BalanceDue -= vm.Amount;
        if (order.BalanceDue <= 0) order.Status = OrderStatus.FullyPaid;
        order.UpdatedBy = _currentUser.UserId;
        order.UpdatedOn = now;

        await _repo.UpdateAsync(order, ct);
        return ApiResponse<object>.Ok((object?)null, "Payment recorded.");
    }

    public async Task<ApiResponse<object>> AddAlterationAsync(CreateOrderAlterationViewModel vm, CancellationToken ct = default)
    {
        var item = await _repo.GetItemByIdAsync(vm.OrderItemId, ct);
        if (item == null) return ApiResponse<object>.Fail("Order item not found.");

        var order = await _repo.GetByIdAsync(item.OrderId, ct);
        if (order == null) return ApiResponse<object>.Fail("Order not found.");

        var now = DateTime.UtcNow;
        order.Alterations.Add(new OrderAlteration
        {
            Id               = Guid.NewGuid(),
            TenantId         = _currentUser.TenantId,
            BranchId         = _currentUser.ShopId,
            OrderId          = order.Id,
            OrderItemId      = vm.OrderItemId,
            Description      = vm.Description,
            AdditionalCharge = vm.AdditionalCharge,
            DeliveryDate     = vm.DeliveryDate,
            CreatedBy        = _currentUser.UserId,
            CreatedOn        = now
        });

        if (vm.AdditionalCharge > 0)
        {
            order.GrandTotal += vm.AdditionalCharge;
            order.BalanceDue += vm.AdditionalCharge;
        }
        order.UpdatedBy = _currentUser.UserId;
        order.UpdatedOn = now;

        await _repo.UpdateAsync(order, ct);
        return ApiResponse<object>.Ok((object?)null, "Alteration added.");
    }

    public async Task<ApiResponse<object>> RecordStageLogAsync(RecordStageLogViewModel vm, CancellationToken ct = default)
    {
        var item = await _repo.GetItemByIdAsync(vm.OrderItemId, ct);
        if (item == null) return ApiResponse<object>.Fail("Order item not found.");

        var now = DateTime.UtcNow;
        item.StageLogs.Add(new OrderItemStageLog
        {
            Id          = Guid.NewGuid(),
            TenantId    = _currentUser.TenantId,
            BranchId    = _currentUser.ShopId,
            OrderItemId = vm.OrderItemId,
            Stage       = vm.Stage,
            KarigarId   = vm.KarigarId,
            Notes       = vm.Notes,
            CreatedBy   = _currentUser.UserId,
            CreatedOn   = now
        });

        item.Status = OrderItemStatus.InProgress;
        await _repo.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok((object?)null, "Stage log recorded.");
    }

    public async Task<ApiResponse<object>> CompleteStageAsync(CompleteStageViewModel vm, CancellationToken ct = default)
    {
        var item = await _repo.GetItemByIdAsync(vm.OrderItemId, ct);
        if (item == null) return ApiResponse<object>.Fail("Order item not found.");

        var log = item.StageLogs
            .Where(l => l.Stage == vm.Stage && !l.CompletedAt.HasValue)
            .OrderByDescending(l => l.CreatedOn)
            .FirstOrDefault();

        if (log == null) return ApiResponse<object>.Fail("No active stage log found for this stage.");

        log.CompletedAt = DateTime.UtcNow;
        if (vm.Notes != null) log.Notes = vm.Notes;

        await _repo.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok((object?)null, "Stage completed.");
    }

    public async Task<ApiResponse<object>> AssignStageAsync(AssignStageViewModel vm, CancellationToken ct = default)
    {
        var item = await _repo.GetItemByIdAsync(vm.OrderItemId, ct);
        if (item == null) return ApiResponse<object>.Fail("Order item not found.");

        var existing = item.StageAssignments.FirstOrDefault(a => a.Stage == vm.Stage);
        if (existing != null)
        {
            existing.AssignedKarigarId = vm.AssignedKarigarId;
            existing.StagePrice        = vm.StagePrice;
            existing.EstimatedDays     = vm.EstimatedDays;
        }
        else
        {
            var now = DateTime.UtcNow;
            item.StageAssignments.Add(new OrderItemStageAssignment
            {
                Id                = Guid.NewGuid(),
                TenantId          = _currentUser.TenantId,
                BranchId          = _currentUser.ShopId,
                OrderItemId       = vm.OrderItemId,
                Stage             = vm.Stage,
                AssignedKarigarId = vm.AssignedKarigarId,
                StagePrice        = vm.StagePrice,
                EstimatedDays     = vm.EstimatedDays,
                CreatedBy         = _currentUser.UserId,
                CreatedOn         = now
            });
        }

        await _repo.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok((object?)null, "Stage assigned.");
    }

    public async Task<ApiResponse<object>> CancelOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order == null) return ApiResponse<object>.Fail("Order not found.");
        if (order.Status == OrderStatus.Delivered) return ApiResponse<object>.Fail("Cannot cancel a delivered order.");

        order.Status    = OrderStatus.Cancelled;
        order.UpdatedBy = _currentUser.UserId;
        order.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(order, ct);
        return ApiResponse<object>.Ok((object?)null, "Order cancelled.");
    }

    public async Task<ApiResponse<object>> MarkDeliveredAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order == null) return ApiResponse<object>.Fail("Order not found.");
        if (order.Status == OrderStatus.Cancelled) return ApiResponse<object>.Fail("Order is cancelled.");

        order.Status    = OrderStatus.Delivered;
        order.UpdatedBy = _currentUser.UserId;
        order.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(order, ct);
        return ApiResponse<object>.Ok((object?)null, "Order marked as delivered.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string GenerateOrderNumber()
        => $"ORD-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    private static void UpdateLoyaltyTier(Customer customer)
    {
        customer.LoyaltyPoints += 10; // 10 points per order
        customer.LoyaltyTier = customer.LoyaltyPoints switch
        {
            < 100  => "Bronze",
            < 500  => "Silver",
            < 1000 => "Gold",
            _      => "Platinum"
        };
    }
}
