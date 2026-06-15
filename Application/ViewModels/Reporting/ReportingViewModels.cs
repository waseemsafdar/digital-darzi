using Domain.Enums;

namespace Application.ViewModels.Reporting;

// ── Dashboard Summary ────────────────────────────────────────────────────
public class DashboardSummaryViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    // Orders
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int OverdueOrders { get; set; }
    public int DueTodayOrders { get; set; }

    // Revenue
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalBalanceDue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSalaries { get; set; }
    public decimal NetProfit { get; set; }

    // Customers
    public int TotalCustomers { get; set; }
    public int NewCustomersThisPeriod { get; set; }

    // Quick stats
    public decimal AverageOrderValue { get; set; }
    public List<DailyRevenueViewModel> DailyRevenue { get; set; } = new();
    public List<TopCustomerViewModel> TopCustomers { get; set; } = new();
    public List<StageQueueCountViewModel> StageQueueCounts { get; set; } = new();
}

// ── Revenue Report ────────────────────────────────────────────────────────
public class RevenueReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalBalanceDue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSalaries { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<DailyRevenueViewModel> DailyBreakdown { get; set; } = new();
    public List<RevenueByGarmentViewModel> ByGarmentType { get; set; } = new();
    public List<RevenueByPaymentMethodViewModel> ByPaymentMethod { get; set; } = new();
}

public class DailyRevenueViewModel
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal Collected { get; set; }
    public decimal UpsellAmount { get; set; }
    public int OrderCount { get; set; }
}

public class RevenueByGarmentViewModel
{
    public GarmentType GarmentType { get; set; }
    public int Count { get; set; }
    public decimal Revenue { get; set; }
}

public class RevenueByPaymentMethodViewModel
{
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

// ── Top Customers ─────────────────────────────────────────────────────────
public class TopCustomerViewModel
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal BalanceDue { get; set; }
    public string LoyaltyTier { get; set; } = "Bronze";
    public int LoyaltyPoints { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

// ── Staff Performance ─────────────────────────────────────────────────────
public class StaffPerformanceViewModel
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public List<string> Roles { get; set; } = new();
    public int OrdersHandled { get; set; }
    public int StagesCompleted { get; set; }
    public decimal TotalKarigarEarnings { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public decimal AttendanceRate { get; set; }
}

// ── Order Status Report ───────────────────────────────────────────────────
public class OrderStatusReportViewModel
{
    public int TotalActive { get; set; }
    public int Pending { get; set; }
    public int Cutting { get; set; }
    public int Stitching { get; set; }
    public int Embroidery { get; set; }
    public int Finishing { get; set; }
    public int ReadyToDeliver { get; set; }
    public int Delivered { get; set; }
    public int Cancelled { get; set; }
    public int Overdue { get; set; }
    public List<StageQueueCountViewModel> StageQueues { get; set; } = new();
}

public class StageQueueCountViewModel
{
    public ProductionStage Stage { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
}

// ── Pending Balances ──────────────────────────────────────────────────────
public class PendingBalanceViewModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime DeliveryDate { get; set; }
    public OrderStatus Status { get; set; }
    public int DaysOverdue { get; set; }
}
