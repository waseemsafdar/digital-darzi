using Application.Common;
using Domain.Enums;

namespace Application.ViewModels.Order;

// ── Create Order ─────────────────────────────────────────────────────────────
public class CreateOrderViewModel
{
    public Guid CustomerId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal Discount { get; set; } = 0;
    public decimal AdvancePayment { get; set; } = 0;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
    public bool TrialRequired { get; set; }
    public DateTime? TrialDate { get; set; }
    public List<CreateOrderItemViewModel> Items { get; set; } = new();
}

public class CreateOrderItemViewModel
{
    public GarmentType GarmentType { get; set; }
    public Guid? MeasurementProfileId { get; set; }                        // use saved profile
    public Guid? TemplateId { get; set; }                                  // or build inline
    public Dictionary<Guid, decimal>? InlineMeasurements { get; set; }     // FieldId → value
    public Dictionary<Guid, string>? InlineTextMeasurements { get; set; }  // FieldId → text (text-type fields)
    public string? FabricDescription { get; set; }
    public string? FabricColor { get; set; }
    public FabricSource FabricSource { get; set; } = FabricSource.CustomerProvided;
    /// <summary>Charge for shop-provided fabric, added to the order subtotal once per item line.</summary>
    public decimal FabricCharge { get; set; }
    public string? StyleNotes { get; set; }
    /// <summary>Garment style attributes, e.g. {"Collar style": "Band"}. Validated against the shop's catalogue.</summary>
    public Dictionary<string, string>? Attributes { get; set; }
    public decimal Price { get; set; }
    public int Qty { get; set; } = 1;
    public List<CreateOrderItemStageAssignmentViewModel>? StageAssignments { get; set; }
}

public class CreateOrderItemStageAssignmentViewModel
{
    public ProductionStage Stage { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    /// <summary>Amount paid to the karigar for this stage. Null = look up the shop's piece rate × quantity.</summary>
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
}

// ── Update Order ─────────────────────────────────────────────────────────────
public class UpdateOrderViewModel : IHasId
{
    public Guid Id { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal? Discount { get; set; }
    public OrderPriority? Priority { get; set; }
    public bool? TrialRequired { get; set; }
    public DateTime? TrialDate { get; set; }
}

// ── Status ───────────────────────────────────────────────────────────────────
public class ChangeOrderStatusViewModel
{
    public OrderStatus Status { get; set; }
    /// <summary>Required when moving the order backwards or forcing a transition.</summary>
    public string? Notes { get; set; }
    /// <summary>Owner/Manager override for a transition outside the normal workflow. Recorded as such.</summary>
    public bool Force { get; set; }
}

// ── Payments ─────────────────────────────────────────────────────────────────
public class RecordOrderPaymentViewModel
{
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Note { get; set; }
}

public class RecordRefundViewModel
{
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Note { get; set; }
}

// ── Cancel / Deliver ─────────────────────────────────────────────────────────
public class CancelOrderViewModel
{
    public string? Reason { get; set; }
    /// <summary>Money returned to the customer now. Must not exceed the amount paid.</summary>
    public decimal RefundAmount { get; set; }
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;
    /// <summary>True when the shop keeps the advance on purpose (e.g. fabric already cut).</summary>
    public bool WaiveRefund { get; set; }
}

public class DeliverOrderViewModel
{
    /// <summary>Items handed over now. Null or empty = every item not yet delivered.</summary>
    public List<Guid>? ItemIds { get; set; }
    /// <summary>Hand over with an unpaid balance. Owner/Manager only; a note is required.</summary>
    public bool AllowBalance { get; set; }
    public string? Notes { get; set; }
}

// ── Trial / fitting ──────────────────────────────────────────────────────────
public class TrialResultViewModel
{
    public TrialOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public List<TrialAlterationViewModel> Alterations { get; set; } = new();
}

public class TrialAlterationViewModel
{
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? RequiredChange { get; set; }
    public decimal AdditionalCharge { get; set; }
    public Guid? AssignedKarigarId { get; set; }
}

// ── Alteration ───────────────────────────────────────────────────────────────
public class CreateOrderAlterationViewModel
{
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? RequiredChange { get; set; }
    public decimal AdditionalCharge { get; set; } = 0;
    public DateTime? DeliveryDate { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAlterationViewModel
{
    public AlterationStatus? Status { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    public string? RequiredChange { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
}

// ── Stage work ───────────────────────────────────────────────────────────────
public class RecordStageLogViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    /// <summary>Who does the work. Empty = the caller. A karigar can only log work for themself.</summary>
    public Guid KarigarId { get; set; }
    public string? Notes { get; set; }
}

public class CompleteStageViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public string? Notes { get; set; }

    // Cutting details (optional; meaningful for the Cutting stage)
    public decimal? FabricUsed { get; set; }
    public int? CutQuantity { get; set; }
    public string? DefectNotes { get; set; }
    public bool RecutRequired { get; set; }
}

public class RejectStageViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Optional deduction from the karigar's ledger for the rejected work.</summary>
    public decimal? DeductAmount { get; set; }
}

public class AssignStageViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public Guid AssignedKarigarId { get; set; }
    /// <summary>Null = the shop's piece rate for this karigar/garment/stage × quantity.</summary>
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
}

// ── Reorder ──────────────────────────────────────────────────────────────────
public class ReorderViewModel
{
    public DateTime DeliveryDate { get; set; }
    /// <summary>True = use each profile's current measurements; false = copy the original order's snapshot.</summary>
    public bool UseLatestMeasurements { get; set; } = true;
}

// ── List / Summary ────────────────────────────────────────────────────────
public class OrderListViewModel
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public OrderPriority Priority { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DateTime? ReadyDate { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class OrderDetailViewModel
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime DeliveryDate { get; set; }
    public DateTime? ReadyDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }
    public OrderPriority Priority { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public List<OrderStatus> AllowedNextStatuses { get; set; } = new();
    public bool TrialRequired { get; set; }
    public DateTime? TrialDate { get; set; }
    public string? TrialNotes { get; set; }
    public string? CancellationReason { get; set; }
    public CancellationStage? CancellationStage { get; set; }
    public decimal RefundAmount { get; set; }
    public RefundStatus? RefundStatus { get; set; }
    public DateTime CreatedOn { get; set; }
    public List<OrderItemDetailViewModel> Items { get; set; } = new();
    public List<OrderPaymentDetailViewModel> Payments { get; set; } = new();
    public List<OrderAlterationViewModel> Alterations { get; set; } = new();
    public List<OrderStatusHistoryViewModel> History { get; set; } = new();
}

public class OrderItemDetailViewModel
{
    public Guid Id { get; set; }
    public GarmentType GarmentType { get; set; }
    public string? FabricDescription { get; set; }
    public string? FabricColor { get; set; }
    public FabricSource FabricSource { get; set; }
    public decimal FabricCharge { get; set; }
    public string? StyleNotes { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public decimal Price { get; set; }
    public int Qty { get; set; }
    public OrderItemStatus Status { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveredOn { get; set; }
    public string? FittingNotes { get; set; }
    public Guid? MeasurementProfileId { get; set; }
    public int? MeasurementProfileVersion { get; set; }
    public string? MeasurementTemplateName { get; set; }
    /// <summary>Legacy shape: field id → numeric value.</summary>
    public Dictionary<string, decimal> MeasurementSnapshot { get; set; } = new();
    /// <summary>Frozen measurements with field names and units (empty for orders created before 2026-09).</summary>
    public List<MeasurementValueViewModel> Measurements { get; set; } = new();
    public List<StageProgressViewModel> StageProgress { get; set; } = new();
}

public class MeasurementValueViewModel
{
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MeasurementUnit? Unit { get; set; }
    public decimal? Value { get; set; }
    public string? Text { get; set; }
}

public class StageProgressViewModel
{
    public ProductionStage Stage { get; set; }
    public Guid? KarigarId { get; set; }
    public string? KarigarName { get; set; }
    public string? Status { get; set; }           // Pending / InProgress / Done / Rework
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ReworkCount { get; set; }
}

public class OrderPaymentDetailViewModel
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Note { get; set; }
    public bool IsRefund { get; set; }
    public DateTime PaidAt { get; set; }
}

public class OrderAlterationViewModel
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? RequiredChange { get; set; }
    public AlterationStatus Status { get; set; }
    public decimal AdditionalCharge { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    public string? AssignedKarigarName { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime? DeliveredOn { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class OrderStatusHistoryViewModel
{
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedOn { get; set; }
    public bool IsReversal { get; set; }
    public bool IsManualOverride { get; set; }
    public Guid? OrderItemId { get; set; }
    public string? Notes { get; set; }
}

// ── Delivery board ───────────────────────────────────────────────────────────
public class DeliveryBoardViewModel
{
    public DateTime Today { get; set; }
    public int DueTodayCount { get; set; }
    public int DueTomorrowCount { get; set; }
    public int OverdueCount { get; set; }
    public int ReadyForPickupCount { get; set; }
    public int DeliveredTodayCount { get; set; }
    public List<OrderListViewModel> DueToday { get; set; } = new();
    public List<OrderListViewModel> DueTomorrow { get; set; } = new();
    public List<OrderListViewModel> Overdue { get; set; } = new();
    public List<OrderListViewModel> ReadyForPickup { get; set; } = new();
    public List<OrderListViewModel> DeliveredToday { get; set; } = new();
}

// ── Receipt ──────────────────────────────────────────────────────────────────
public class OrderReceiptViewModel
{
    public string ShopName { get; set; } = string.Empty;
    public string? ShopPhone { get; set; }
    public string CurrencySymbol { get; set; } = "₨";
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public List<OrderReceiptLineViewModel> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Alterations { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public List<OrderPaymentDetailViewModel> Payments { get; set; } = new();
    public OrderStatus Status { get; set; }
}

public class OrderReceiptLineViewModel
{
    public string Description { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

// ── Search Filter ─────────────────────────────────────────────────────────
public class OrderSearchViewModel
{
    public string? Query { get; set; }          // order number / customer name / customer phone
    public OrderStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderPriority? Priority { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? KarigarId { get; set; }        // orders with any stage assigned to this karigar
    public GarmentType? GarmentType { get; set; }
    public DateTime? DateFrom { get; set; }     // created on/after
    public DateTime? DateTo { get; set; }       // created on/before
    public DateTime? DeliveryFrom { get; set; }
    public DateTime? DeliveryTo { get; set; }
    public bool? DueToday { get; set; }
    public bool? Overdue { get; set; }
    public bool? HasBalance { get; set; }
    /// <summary>createdOn (default), deliveryDate, grandTotal, balanceDue, orderNumber.</summary>
    public string? SortBy { get; set; }
    /// <summary>asc or desc (default desc).</summary>
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
