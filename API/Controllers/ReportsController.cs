using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "SystemAdmin,Owner,Manager")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _service;
    public ReportsController(IReportingService service) => _service = service;

    /// <summary>Full dashboard summary — orders, revenue, customers, stage counts.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetDashboardAsync(dateFrom, dateTo, ct));
    }

    /// <summary>Revenue P&L report — by garment type, payment method, daily breakdown.</summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetRevenueReportAsync(dateFrom, dateTo, ct));
    }

    /// <summary>Daily revenue breakdown chart data.</summary>
    [HttpGet("daily-revenue")]
    public async Task<IActionResult> GetDailyRevenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetDailyRevenueAsync(dateFrom, dateTo, ct));
    }

    /// <summary>Top customers by spend in period.</summary>
    [HttpGet("top-customers")]
    public async Task<IActionResult> GetTopCustomers(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetTopCustomersAsync(dateFrom, dateTo, limit, ct));
    }

    /// <summary>Staff performance — stages completed, earnings, attendance rate.</summary>
    [HttpGet("staff-performance")]
    public async Task<IActionResult> GetStaffPerformance(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo   = to   ?? DateTime.UtcNow;
        return Ok(await _service.GetStaffPerformanceAsync(dateFrom, dateTo, ct));
    }

    /// <summary>Current order status counts + stage queues.</summary>
    [HttpGet("order-status")]
    public async Task<IActionResult> GetOrderStatus(CancellationToken ct)
        => Ok(await _service.GetOrderStatusReportAsync(ct));

    /// <summary>All orders with outstanding balance due.</summary>
    [HttpGet("pending-balances")]
    public async Task<IActionResult> GetPendingBalances(CancellationToken ct)
        => Ok(await _service.GetPendingBalancesAsync(ct));
}
