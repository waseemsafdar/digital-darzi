using Domain.Enums;
using Microsoft.AspNetCore.Http;

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
    public List<CreateOrderItemViewModel> Items { get; set; } = new();
    public List<IFormFile>? Attachments { get; set; }
}

public class CreateOrderItemViewModel
{
    public GarmentType GarmentType { get; set; }
    public Guid? MeasurementProfileId { get; set; }   // use saved profile
    public Guid? TemplateId { get; set; }               // or build inline
    public Dictionary<Guid, decimal>? InlineMeasurements { get; set; }   // FieldId→value
    public string? FabricDescription { get; set; }
    public string? FabricColor { get; set; }
    public string? StyleNotes { get; set; }
    public decimal Price { get; set; }
    public int Qty { get; set; } = 1;
    public List<CreateOrderItemStageAssignmentViewModel>? StageAssignments { get; set; }
}

public class CreateOrderItemStageAssignmentViewModel
{
    public ProductionStage Stage { get; set; }
    public Guid? AssignedKarigarId { get; set; }
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
}

// ── Update Order ─────────────────────────────────────────────────────────────
public class UpdateOrderViewModel
{
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal? Discount { get; set; }
}

// ── Record Payment ─────────────────────────────────────────────────────────
public class RecordOrderPaymentViewModel
{
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Note { get; set; }
    public DateTime? PaidAt { get; set; }
}

// ── Alteration ────────────────────────────────────────────────────────────
public class CreateOrderAlterationViewModel
{
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AdditionalCharge { get; set; } = 0;
    public DateTime? DeliveryDate { get; set; }
}

// ── Stage Log ─────────────────────────────────────────────────────────────
public class RecordStageLogViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public Guid KarigarId { get; set; }
    public string? Notes { get; set; }
}

// ── Complete Stage ────────────────────────────────────────────────────────
public class CompleteStageViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public string? Notes { get; set; }
}

// ── Assign Stage ─────────────────────────────────────────────────────────
public class AssignStageViewModel
{
    public Guid OrderItemId { get; set; }
    public ProductionStage Stage { get; set; }
    public Guid AssignedKarigarId { get; set; }
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
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
    public DateTime DeliveryDate { get; set; }
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
    public string? Notes { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public List<OrderItemDetailViewModel> Items { get; set; } = new();
    public List<OrderPaymentDetailViewModel> Payments { get; set; } = new();
    public List<OrderAlterationViewModel> Alterations { get; set; } = new();
}

public class OrderItemDetailViewModel
{
    public Guid Id { get; set; }
    public GarmentType GarmentType { get; set; }
    public string? FabricDescription { get; set; }
    public string? FabricColor { get; set; }
    public string? StyleNotes { get; set; }
    public decimal Price { get; set; }
    public int Qty { get; set; }
    public OrderItemStatus Status { get; set; }
    public Dictionary<string, decimal> MeasurementSnapshot { get; set; } = new();
    public List<StageProgressViewModel> StageProgress { get; set; } = new();
}

public class StageProgressViewModel
{
    public ProductionStage Stage { get; set; }
    public string? KarigarName { get; set; }
    public string? Status { get; set; }           // Pending / InProgress / Done
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class OrderPaymentDetailViewModel
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Note { get; set; }
    public DateTime PaidAt { get; set; }
}

public class OrderAlterationViewModel
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AdditionalCharge { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime CreatedOn { get; set; }
}

// ── Search Filter ─────────────────────────────────────────────────────────
public class OrderSearchViewModel
{
    public string? Query { get; set; }          // order number / customer name
    public OrderStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public bool? DueToday { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
