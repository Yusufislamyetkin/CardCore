using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.EodService.Api.Dtos;
using MiniBank.EodService.Domain.Entities;
using MiniBank.EodService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.EodService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/eod")]
public sealed class EodController : ControllerBase
{
    private readonly EodDbContext _db;

    public EodController(EodDbContext db) => _db = db;

    [HttpPost("runs")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleEodRunRequest request, CancellationToken ct)
    {
        if (await _db.EodRuns.AnyAsync(r => r.ProcessingDate == request.ProcessingDate, ct))
            return Conflict(new { error = $"EOD run for {request.ProcessingDate} already exists" });

        var run = EodRun.Schedule(request.ProcessingDate);
        _db.EodRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = run.Id.Value }, ToRunResponse(run));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var run = await _db.EodRuns.FindAsync(EodRunId.From(id), ct);
        return run is null ? NotFound() : Ok(ToRunResponse(run));
    }

    [HttpGet("runs/date/{date}")]
    public async Task<IActionResult> GetByDate(DateOnly date, CancellationToken ct)
    {
        var run = await _db.EodRuns.FirstOrDefaultAsync(r => r.ProcessingDate == date, ct);
        return run is null ? NotFound() : Ok(ToRunResponse(run));
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 30, CancellationToken ct = default)
    {
        var runs = await _db.EodRuns
            .OrderByDescending(r => r.ProcessingDate)
            .Take(limit)
            .ToListAsync(ct);
        return Ok(runs.Select(ToRunResponse));
    }

    [HttpPut("runs/{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var run = await _db.EodRuns.FindAsync(EodRunId.From(id), ct);
        if (run is null) return NotFound();

        run.Start();
        await _db.SaveChangesAsync(ct);
        return Ok(ToRunResponse(run));
    }

    [HttpPut("runs/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteEodRunRequest request, CancellationToken ct)
    {
        var run = await _db.EodRuns.FindAsync(EodRunId.From(id), ct);
        if (run is null) return NotFound();

        run.Complete(request.ProcessedTransactionCount, request.TotalSettledAmount);
        await _db.SaveChangesAsync(ct);
        return Ok(ToRunResponse(run));
    }

    [HttpPut("runs/{id:guid}/fail")]
    public async Task<IActionResult> Fail(Guid id, [FromBody] FailEodRunRequest request, CancellationToken ct)
    {
        var run = await _db.EodRuns.FindAsync(EodRunId.From(id), ct);
        if (run is null) return NotFound();

        run.Fail(request.ErrorMessage);
        await _db.SaveChangesAsync(ct);
        return Ok(ToRunResponse(run));
    }

    [HttpPost("runs/{id:guid}/statements")]
    public async Task<IActionResult> GenerateStatement(
        Guid id, [FromBody] GenerateStatementRequest request, CancellationToken ct)
    {
        var run = await _db.EodRuns.FindAsync(EodRunId.From(id), ct);
        if (run is null) return NotFound(new { error = "EOD run not found" });
        if (run.Status != EodRunStatus.Running && run.Status != EodRunStatus.Completed)
            return BadRequest(new { error = "Statements can only be generated for running or completed runs" });

        if (!Guid.TryParse(request.AccountId, out var accountGuid))
            return BadRequest(new { error = "Invalid AccountId" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        var accountId = AccountId.From(accountGuid);
        if (await _db.Statements.AnyAsync(s =>
            s.AccountId == accountId && s.Year == request.Year && s.Month == request.Month, ct))
            return Conflict(new { error = $"Statement for account {request.AccountId} {request.Year}/{request.Month:D2} already exists" });

        var statement = Statement.Generate(
            accountId,
            run.Id,
            request.Year, request.Month,
            new Money(request.OpeningBalanceAmount, currency),
            new Money(request.ClosingBalanceAmount, currency),
            new Money(request.TotalDebitsAmount, currency),
            new Money(request.TotalCreditsAmount, currency),
            request.TransactionCount);

        _db.Statements.Add(statement);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetStatementById), new { stmtId = statement.Id.Value }, ToStatementResponse(statement));
    }

    [HttpGet("statements/{stmtId:guid}")]
    public async Task<IActionResult> GetStatementById(Guid stmtId, CancellationToken ct)
    {
        var stmt = await _db.Statements.FindAsync(StatementId.From(stmtId), ct);
        return stmt is null ? NotFound() : Ok(ToStatementResponse(stmt));
    }

    [HttpGet("statements/account/{accountId:guid}")]
    public async Task<IActionResult> GetStatementsByAccount(Guid accountId, CancellationToken ct)
    {
        var stmts = await _db.Statements
            .Where(s => s.AccountId == AccountId.From(accountId))
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .ToListAsync(ct);
        return Ok(stmts.Select(ToStatementResponse));
    }

    private static EodRunResponse ToRunResponse(EodRun r) => new(
        r.Id.Value, r.ProcessingDate, r.Status.ToString(),
        r.StartedAt, r.CompletedAt, r.ProcessedTransactionCount,
        r.TotalSettledAmount, r.ErrorMessage, r.CreatedAt);

    private static StatementResponse ToStatementResponse(Statement s) => new(
        s.Id.Value, s.AccountId.Value, s.GeneratedByRunId.Value,
        s.Year, s.Month,
        s.OpeningBalance.Amount, s.ClosingBalance.Amount,
        s.TotalDebits.Amount, s.TotalCredits.Amount,
        s.OpeningBalance.Currency.ToString(),
        s.TransactionCount, s.CreatedAt);
}
