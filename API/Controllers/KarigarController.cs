using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Enums;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/karigar")]
[Authorize]
public class KarigarController : BaseController
{
    private readonly IKarigarService _service;
    private readonly IKarigarLedgerService _ledger;
    private readonly ICurrentUserService _currentUser;

    public KarigarController(IKarigarService service, IKarigarLedgerService ledger, ICurrentUserService currentUser)
    {
        _service = service;
        _ledger = ledger;
        _currentUser = currentUser;
    }

    /// <summary>The signed-in karigar's open stage work (pending, in progress, rework) and alterations.</summary>
    [HttpGet("my-queue")]
    public async Task<IActionResult> GetMyQueue(CancellationToken ct) => ReturnProcessedResponse(await _service.GetMyWorkQueueAsync(ct));

    [HttpGet("my-completed")]
    public async Task<IActionResult> GetMyCompleted([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetMyCompletedAsync(from, to, ct));

    /// <summary>The signed-in karigar's own earnings, advances, deductions and payouts.</summary>
    [HttpGet("my-ledger")]
    public async Task<IActionResult> GetMyLedger([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => ReturnProcessedResponse(await _ledger.GetLedgerAsync(_currentUser.UserId, from, to, ct));

    [HttpGet("{karigarId:guid}/queue")]
    [HasPermission(Permissions.KarigarViewAll)]
    public async Task<IActionResult> GetKarigarQueue(Guid karigarId, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetKarigarWorkQueueAsync(karigarId, ct));

    [HttpGet("performance")]
    [HasPermission(Permissions.KarigarViewAll)]
    public async Task<IActionResult> GetPerformanceSummary(CancellationToken ct) => ReturnProcessedResponse(await _service.GetAllKarigarsSummaryAsync(ct));

    // ── Piece rates ──────────────────────────────────────────────────────────

    [HttpGet("rates")]
    [HasPermission(Permissions.KarigarViewAll)]
    public async Task<IActionResult> GetRates([FromQuery] Guid? karigarId, CancellationToken ct)
        => ReturnProcessedResponse(await _ledger.GetRatesAsync(karigarId, ct));

    /// <summary>Create or change a piece rate (karigarId null = shop default).</summary>
    [HttpPut("rates")]
    [HasPermission(Permissions.KarigarPayouts)]
    public async Task<IActionResult> UpsertRate([FromBody] UpsertKarigarRateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _ledger.UpsertRateAsync(vm, ct));

    [HttpDelete("rates/{id:guid}")]
    [HasPermission(Permissions.KarigarPayouts)]
    public async Task<IActionResult> DeleteRate(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _ledger.DeleteRateAsync(id, ct));

    // ── Ledger / payouts ─────────────────────────────────────────────────────

    [HttpGet("balances")]
    [HasPermission(Permissions.KarigarPayouts)]
    public async Task<IActionResult> GetBalances(CancellationToken ct) => ReturnProcessedResponse(await _ledger.GetBalancesAsync(ct));

    [HttpGet("{karigarId:guid}/ledger")]
    [HasPermission(Permissions.KarigarPayouts)]
    public async Task<IActionResult> GetLedger(Guid karigarId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => ReturnProcessedResponse(await _ledger.GetLedgerAsync(karigarId, from, to, ct));

    /// <summary>Record a payout, advance, deduction or adjustment. Earnings are added automatically when stages complete.</summary>
    [HttpPost("{karigarId:guid}/ledger")]
    [HasPermission(Permissions.KarigarPayouts)]
    public async Task<IActionResult> RecordEntry(Guid karigarId, [FromBody] RecordKarigarLedgerEntryViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _ledger.RecordEntryAsync(karigarId, vm, ct));
}

/// <summary>Production boards per stage (Kanban columns).</summary>
[ApiController]
[Route("api/workflow")]
[Authorize]
public class WorkflowController : BaseController
{
    private readonly IWorkBoardService _board;
    public WorkflowController(IWorkBoardService board) => _board = board;

    /// <summary>Columns: Unassigned, Assigned, InProgress, Rework, CompletedRecently — with late / due-today flags.</summary>
    [HttpGet("board")]
    [HasPermission(Permissions.ProductionAssign)]
    public async Task<IActionResult> GetBoard([FromQuery] ProductionStage stage = ProductionStage.Stitching, [FromQuery] int perColumn = 50, CancellationToken ct = default)
        => ReturnProcessedResponse(await _board.GetBoardAsync(stage, perColumn, ct));
}
