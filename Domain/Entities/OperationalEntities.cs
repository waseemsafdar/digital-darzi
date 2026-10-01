using Domain.Enums;

namespace Domain.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// Branch-scoped operational records added in the 2026-09 roadmap phases.
// All derive from BaseDBModel and are covered by the TenantId + BranchId filter.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Immutable copy of a measurement profile's values at a point in time.</summary>
public class MeasurementProfileVersion : BaseDBModel
{
    public Guid ProfileId { get; set; }
    public int VersionNo { get; set; }
    public Guid TemplateId { get; set; }
    public string? FieldValuesJson { get; set; }
    public string? TextValuesJson { get; set; }
    public string? Notes { get; set; }

    public MeasurementProfile Profile { get; set; } = null!;
}

/// <summary>Configurable style attribute for a garment type, e.g. "Collar style" with options.</summary>
public class GarmentAttribute : BaseDBModel
{
    public GarmentType? GarmentType { get; set; }   // null = applies to all garment types
    public string Name { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }        // JSONB string[]; null/empty = free text
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Piece rate paid to karigars for a stage of a garment type.</summary>
public class KarigarRate : BaseDBModel
{
    public Guid? KarigarId { get; set; }            // null = shop default for everyone
    public GarmentType GarmentType { get; set; }
    public ProductionStage Stage { get; set; }
    public decimal Rate { get; set; }               // per garment piece
}

/// <summary>Signed ledger line for a karigar. Balance payable = Σ SignedAmount.</summary>
public class KarigarLedgerEntry : BaseDBModel
{
    public Guid KarigarId { get; set; }
    public KarigarLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }             // always positive as entered
    public decimal SignedAmount { get; set; }       // + increases payable, - decreases
    public DateTime EntryDate { get; set; }
    public Guid? OrderItemId { get; set; }
    public ProductionStage? Stage { get; set; }
    public Guid? StageLogId { get; set; }           // unique for Earning lines → idempotent
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class NotificationTemplate : BaseDBModel
{
    public NotificationEvent EventType { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public string Body { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Outbox row: written in the same SaveChanges as the business change, delivered by a background worker.</summary>
public class NotificationMessage : BaseDBModel
{
    public NotificationEvent EventType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ProviderMessageId { get; set; }
}

public class Supplier : BaseDBModel
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}

public class InventoryItem : BaseDBModel
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

    /// <summary>Cached Σ of movements, maintained in the same transaction as each movement.</summary>
    public decimal QuantityOnHand { get; set; }

    [System.ComponentModel.DataAnnotations.Timestamp]
    public uint Version { get; set; }

    public Supplier? Supplier { get; set; }
}

/// <summary>Append-only stock ledger. Quantity is signed (+ in, - out).</summary>
public class InventoryMovement : BaseDBModel
{
    public Guid ItemId { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime MovementDate { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CounterpartBranchId { get; set; }     // transfers
    public Guid? ExpenseId { get; set; }               // purchase recorded as an expense
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public InventoryItem Item { get; set; } = null!;
}

public class CashSession : BaseDBModel
{
    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;
    public Guid OpenedBy { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Variance { get; set; }
    public string? Notes { get; set; }
}

public class CashMovement : BaseDBModel
{
    public Guid SessionId { get; set; }
    public CashMovementDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime MovementAt { get; set; }
}

/// <summary>Salary terms for one staff member; drives the attendance-based salary calculation.</summary>
public class SalaryRule : BaseDBModel
{
    public Guid StaffUserId { get; set; }
    public decimal MonthlySalary { get; set; }
    public int WorkingDaysPerMonth { get; set; } = 26;
    public int PaidLeaveDays { get; set; }
    public decimal HalfDayFactor { get; set; } = 0.5m;
}

public class SalaryAdvance : BaseDBModel
{
    public Guid StaffUserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AdvanceDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
    public Guid? RecoveredInSalaryId { get; set; }
}

public class ExpenseCategoryDefinition : BaseDBModel
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
