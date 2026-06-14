using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/karigar")]
[Authorize]
public class KarigarController : ControllerBase
{
    private readonly IKarigarService _service;
    public KarigarController(IKarigarService service) => _service = service;

    /// <summary>Returns the logged-in karigar's own work queue.</summary>
    [HttpGet("my-queue")]
    public async Task<IActionResult> GetMyQueue(CancellationToken ct)
        => Ok(await _service.GetMyWorkQueueAsync(ct));

    /// <summary>Returns work queue for a specific karigar (Owner/Manager only).</summary>
    [HttpGet("{karigarId:guid}/queue")]
    [Authorize(Roles = "SystemAdmin,Owner,Manager")]
    public async Task<IActionResult> GetKarigarQueue(Guid karigarId, CancellationToken ct)
        => Ok(await _service.GetKarigarWorkQueueAsync(karigarId, ct));

    /// <summary>Returns performance summary for all karigars (Owner/Manager only).</summary>
    [HttpGet("performance")]
    [Authorize(Roles = "SystemAdmin,Owner,Manager")]
    public async Task<IActionResult> GetPerformanceSummary(CancellationToken ct)
        => Ok(await _service.GetAllKarigarsSummaryAsync(ct));
}
