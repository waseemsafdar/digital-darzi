using Domain.Enums;

namespace Application.ViewModels.Operations;

// ── Garment attributes ───────────────────────────────────────────────────────
public class GarmentAttributeViewModel
{
    public Guid Id { get; set; }
    public GarmentType? GarmentType { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

public class SaveGarmentAttributeViewModel
{
    /// <summary>Null = applies to every garment type.</summary>
    public GarmentType? GarmentType { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Allowed values; empty = free text.</summary>
    public List<string> Options { get; set; } = new();
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

// ── Garment types ────────────────────────────────────────────────────────────
public class GarmentTypeViewModel
{
    /// <summary>The value stored on orders, rates and styles (1–99 built-in, 100+ the shop's own).</summary>
    public int Code { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>The starter name for a built-in type; null for the shop's own types.</summary>
    public string? DefaultName { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsHidden { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Order items using this type. Only filled when hidden types are requested (the manage view).</summary>
    public int? UsedByItems { get; set; }
}

public class CreateGarmentTypeViewModel
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateGarmentTypeViewModel
{
    public string Name { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
}

public class ReorderGarmentTypesViewModel
{
    /// <summary>Every code in the new display order; codes left out keep their place after these.</summary>
    public List<int> Codes { get; set; } = new();
}

// ── Inventory ────────────────────────────────────────────────────────────────
public class SupplierViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}

public class SaveSupplierViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}

public class InventoryItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public InventoryCategory Category { get; set; }
    public InventoryUnit Unit { get; set; }
    public string? Color { get; set; }
    public string? Design { get; set; }
    public decimal? Width { get; set; }
    public string? Location { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal StockValue { get; set; }
    public bool IsLowStock { get; set; }
}

public class SaveInventoryItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public InventoryCategory Category { get; set; } = InventoryCategory.Fabric;
    public InventoryUnit Unit { get; set; } = InventoryUnit.Meter;
    public string? Color { get; set; }
    public string? Design { get; set; }
    public decimal? Width { get; set; }
    public string? Location { get; set; }
    public Guid? SupplierId { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
}

public class InventorySearchViewModel
{
    public string? Query { get; set; }
    public InventoryCategory? Category { get; set; }
    public bool? LowStockOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class InventoryMovementViewModel
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public InventoryMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime MovementDate { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CounterpartBranchId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedBy { get; set; }
}

public class RecordPurchaseViewModel
{
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    /// <summary>Also record the purchase as a shop expense (category "Supplies").</summary>
    public bool RecordAsExpense { get; set; } = true;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
}

public class IssueToOrderViewModel
{
    public Guid OrderItemId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class ReturnFromOrderViewModel
{
    public Guid OrderItemId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class StockAdjustmentViewModel
{
    /// <summary>Adjustment (signed), Waste (positive = removed) or SupplierReturn (positive = removed).</summary>
    public InventoryMovementType MovementType { get; set; } = InventoryMovementType.Adjustment;
    public decimal Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class StockTransferViewModel
{
    public Guid TargetShopId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

// ── Cash register ────────────────────────────────────────────────────────────
public class OpenCashSessionViewModel
{
    public decimal OpeningFloat { get; set; }
    public string? Notes { get; set; }
}

public class CashMovementRequestViewModel
{
    public CashMovementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CloseCashSessionViewModel
{
    public decimal CountedCash { get; set; }
    public string? Notes { get; set; }
}

public class CashSessionViewModel
{
    public Guid Id { get; set; }
    public CashSessionStatus Status { get; set; }
    public Guid OpenedBy { get; set; }
    public string? OpenedByName { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal CashExpenses { get; set; }
    public decimal CashSalaries { get; set; }
    public decimal CashKarigarPayments { get; set; }
    public decimal ManualIn { get; set; }
    public decimal ManualOut { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Variance { get; set; }
    public string? Notes { get; set; }
    public List<CashMovementItemViewModel> Movements { get; set; } = new();
}

public class CashMovementItemViewModel
{
    public Guid Id { get; set; }
    public CashMovementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime MovementAt { get; set; }
}

// ── Audit ────────────────────────────────────────────────────────────────────
public class AuditLogViewModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class AuditSearchViewModel
{
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ── Production board ─────────────────────────────────────────────────────────
public class WorkBoardViewModel
{
    public ProductionStage Stage { get; set; }
    public List<WorkBoardCardViewModel> Unassigned { get; set; } = new();
    public List<WorkBoardCardViewModel> Assigned { get; set; } = new();
    public List<WorkBoardCardViewModel> InProgress { get; set; } = new();
    public List<WorkBoardCardViewModel> Rework { get; set; } = new();
    public List<WorkBoardCardViewModel> CompletedRecently { get; set; } = new();
}

public class WorkBoardCardViewModel
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public GarmentType GarmentType { get; set; }
    public int Qty { get; set; }
    public OrderPriority Priority { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool IsLate { get; set; }
    public bool DueToday { get; set; }
    public Guid? KarigarId { get; set; }
    public string? KarigarName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReworkReason { get; set; }
}
