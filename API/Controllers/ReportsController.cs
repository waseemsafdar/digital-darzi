using Infrastructure.Services;
using System.Globalization;
using System.Text;
using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Reports for the signed-in branch. Add <c>?scope=tenant</c> (Owner) to consolidate every branch.
/// Date ranges default to the last 30 days.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[HasPermission(Permissions.ReportsView)]
public class ReportsController : BaseController
{
    private readonly IReportingService _service;
    private readonly IFinanceService _finance;
    private readonly TimeProvider _clock;

    public ReportsController(IReportingService service, IFinanceService finance, TimeProvider clock)
    {
        _service = service;
        _finance = finance;
        _clock = clock;
    }

    private (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return (from ?? now.AddDays(-30), to ?? now);
    }

    /// <summary>Returns (tenantWide, forbiddenResult).</summary>
    private async Task<(bool TenantWide, IActionResult? Denied)> ScopeAsync(string? scope)
    {
        if (!string.Equals(scope, "tenant", StringComparison.OrdinalIgnoreCase)) return (false, null);
        return await HasPermissionAsync(Permissions.ReportsTenantWide)
            ? (true, null)
            : (false, Forbidden("Only the Owner can view reports across all branches."));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetDashboardAsync(f, t, tenantWide, ct));
    }

    [HttpGet("revenue")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetRevenueReportAsync(f, t, tenantWide, ct));
    }

    [HttpGet("daily-revenue")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> GetDailyRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetDailyRevenueAsync(f, t, tenantWide, ct));
    }

    [HttpGet("top-customers")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> GetTopCustomers([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int limit = 10, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetTopCustomersAsync(f, t, limit, tenantWide, ct));
    }

    [HttpGet("staff-performance")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> GetStaffPerformance([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetStaffPerformanceAsync(f, t, tenantWide, ct));
    }

    [HttpGet("order-status")]
    public async Task<IActionResult> GetOrderStatus([FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        return ReturnProcessedResponse(await _service.GetOrderStatusReportAsync(tenantWide, ct));
    }

    [HttpGet("pending-balances")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> GetPendingBalances([FromQuery] int limit = 200, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        return ReturnProcessedResponse(await _service.GetPendingBalancesAsync(limit, tenantWide, ct));
    }

    /// <summary>Side-by-side figures for every branch (Owner).</summary>
    [HttpGet("branches")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    [HasPermission(Permissions.ReportsTenantWide)]
    public async Task<IActionResult> GetBranches([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return ReturnProcessedResponse(await _service.GetBranchComparisonAsync(f, t, ct));
    }

    /// <summary>
    /// CSV download (opens in Excel): daily-revenue, top-customers, pending-balances,
    /// staff-performance, expenses, branches.
    /// </summary>
    [HttpGet("export/{report}")]
    [RequiresPlanFeature(PlanFeature.Reports)]
    public async Task<IActionResult> Export(string report, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? scope, CancellationToken ct)
    {
        var (tenantWide, denied) = await ScopeAsync(scope);
        if (denied != null) return denied;
        var (f, t) = Range(from, to);

        string csv;
        switch (report.ToLowerInvariant())
        {
            case "daily-revenue":
                csv = Csv.Write(new[] { "Date", "Orders", "Revenue", "Collected", "Upsell" },
                    ((await _service.GetDailyRevenueAsync(f, t, tenantWide, ct)).Data ?? new())
                    .Select(r => new object?[] { r.Date.ToString("yyyy-MM-dd"), r.OrderCount, r.Revenue, r.Collected, r.UpsellAmount }));
                break;
            case "top-customers":
                csv = Csv.Write(new[] { "Customer", "Phone", "Orders", "TotalSpend", "BalanceDue", "Tier", "LastOrder" },
                    ((await _service.GetTopCustomersAsync(f, t, 200, tenantWide, ct)).Data ?? new())
                    .Select(c => new object?[] { c.Name, c.Phone, c.OrderCount, c.TotalSpend, c.BalanceDue, c.LoyaltyTier, c.LastOrderDate?.ToString("yyyy-MM-dd") }));
                break;
            case "pending-balances":
                csv = Csv.Write(new[] { "Order", "Customer", "Phone", "Total", "Paid", "Balance", "DeliveryDate", "Status", "DaysOverdue" },
                    ((await _service.GetPendingBalancesAsync(1000, tenantWide, ct)).Data ?? new())
                    .Select(p => new object?[] { p.OrderNumber, p.CustomerName, p.CustomerPhone, p.GrandTotal, p.AmountPaid, p.BalanceDue, p.DeliveryDate.ToString("yyyy-MM-dd"), p.Status, p.DaysOverdue }));
                break;
            case "staff-performance":
                csv = Csv.Write(new[] { "Name", "Phone", "Roles", "StagesCompleted", "Earnings", "PresentDays", "AbsentDays", "AttendanceRate" },
                    ((await _service.GetStaffPerformanceAsync(f, t, tenantWide, ct)).Data ?? new())
                    .Select(s => new object?[] { s.Name, s.Phone, string.Join("|", s.Roles), s.StagesCompleted, s.TotalKarigarEarnings, s.PresentDays, s.AbsentDays, s.AttendanceRate }));
                break;
            case "expenses":
                csv = Csv.Write(new[] { "Date", "Category", "Description", "Amount", "PaymentMethod", "Receipt", "AddedBy" },
                    ((await _finance.GetExpensesAsync(f, t, null, 1, Paging.MaxPageSize, ct)).Data?.Items ?? new List<ShopExpenseListViewModel>())
                    .Select(e => new object?[] { e.ExpenseDate.ToString("yyyy-MM-dd"), e.Category, e.Description, e.Amount, e.PaymentMethod, e.ReceiptRef, e.AddedByName }));
                break;
            case "branches":
                if (!await HasPermissionAsync(Permissions.ReportsTenantWide)) return Forbidden("Only the Owner can export branch figures.");
                csv = Csv.Write(new[] { "Branch", "Orders", "Revenue", "Collected", "Expenses", "Outstanding", "ActiveOrders" },
                    ((await _service.GetBranchComparisonAsync(f, t, ct)).Data ?? new())
                    .Select(b => new object?[] { b.ShopName, b.Orders, b.Revenue, b.Collected, b.Expenses, b.OutstandingBalance, b.ActiveOrders }));
                break;
            default:
                return NotFound(ApiResponse<object>.NotFound($"Unknown report '{report}'."));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();   // BOM so Excel reads UTF-8
        return File(bytes, "text/csv", $"{report}-{f:yyyyMMdd}-{t:yyyyMMdd}.csv");
    }
}

/// <summary>RFC 4180 CSV with spreadsheet-formula neutralisation.</summary>
internal static class Csv
{
    public static string Write(IEnumerable<string> headers, IEnumerable<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Cell)));
        foreach (var row in rows) sb.AppendLine(string.Join(",", row.Select(Cell)));
        return sb.ToString();
    }

    internal static string Cell(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        // A cell starting with = + - @ is executed as a formula by Excel; prefix it so it stays text.
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]) && value is not decimal && value is not int)
            text = "'" + text;
        return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
