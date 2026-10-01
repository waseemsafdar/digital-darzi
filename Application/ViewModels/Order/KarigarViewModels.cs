using Domain.Enums;

namespace Application.ViewModels.Order;

// ── Karigar Work Queue ────────────────────────────────────────────────────
public class KarigarWorkQueueViewModel
{
    public Guid KarigarId { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public int TotalPending { get; set; }
    public int TotalInProgress { get; set; }
    public int TotalCompletedToday { get; set; }
    public int TotalRework { get; set; }
    public List<KarigarStageTaskViewModel> PendingTasks { get; set; } = new();
    public List<KarigarStageTaskViewModel> InProgressTasks { get; set; } = new();
    public List<OrderAlterationViewModel> AlterationTasks { get; set; } = new();
}

public class KarigarStageTaskViewModel
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public GarmentType GarmentType { get; set; }
    public ProductionStage Stage { get; set; }
    public DateTime OrderDeliveryDate { get; set; }
    /// <summary>Due today or tomorrow (shop-local), or already late.</summary>
    public bool IsUrgent { get; set; }
    public bool IsOverdue { get; set; }
    public OrderPriority Priority { get; set; }
    public bool IsRework { get; set; }
    public string? ReworkReason { get; set; }
    public int Qty { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public decimal? StagePrice { get; set; }
    public int EstimatedDays { get; set; }
    public string? StyleNotes { get; set; }
    public string? FabricColor { get; set; }
    public DateTime AssignedOn { get; set; }
}

// ── Karigar Performance ──────────────────────────────────────────────────
public class KarigarPerformanceSummaryViewModel
{
    public Guid KarigarId { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int TotalAssigned { get; set; }
    public int TotalCompleted { get; set; }
    public int TotalPending { get; set; }
    public int TotalInProgress { get; set; }
    public int TotalRework { get; set; }
    public decimal CompletionRate => TotalAssigned > 0
        ? Math.Round((decimal)TotalCompleted / TotalAssigned * 100, 1) : 0;
    /// <summary>Earned from completed work (ledger earnings), not from assignments.</summary>
    public decimal TotalEarnings { get; set; }
    public decimal Balance { get; set; }
    public List<StageBreakdownViewModel> StageBreakdown { get; set; } = new();
}

public class StageBreakdownViewModel
{
    public ProductionStage Stage { get; set; }
    public int Count { get; set; }
    public decimal Earnings { get; set; }
}

// ── Karigar Completed History ─────────────────────────────────────────────
public class KarigarCompletedHistoryViewModel
{
    public Guid KarigarId { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public int TotalCompleted { get; set; }
    public decimal TotalEarnings { get; set; }
    public List<KarigarCompletedStageViewModel> CompletedStages { get; set; } = new();
}

public class KarigarCompletedStageViewModel
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public GarmentType GarmentType { get; set; }
    public ProductionStage Stage { get; set; }
    public decimal? StagePrice { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    /// <summary>Duration in minutes.</summary>
    public double DurationMinutes => (CompletedAt - StartedAt).TotalMinutes;
}
