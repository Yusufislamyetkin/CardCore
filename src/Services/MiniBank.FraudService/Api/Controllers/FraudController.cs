using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.FraudService.Api.Dtos;
using MiniBank.FraudService.Application;
using MiniBank.FraudService.Domain.Entities;
using MiniBank.FraudService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.FraudService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/fraud")]
public sealed class FraudController : ControllerBase
{
    private readonly FraudDbContext _db;
    private readonly FraudScoringEngine _engine;

    public FraudController(FraudDbContext db, FraudScoringEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateFraudRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.TransactionId, out var txGuid) ||
            !Guid.TryParse(request.CardId, out var cardGuid) ||
            !Guid.TryParse(request.AccountId, out var accountGuid))
            return BadRequest(new { error = "Invalid TransactionId, CardId, or AccountId" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        var scoringRequest = new FraudScoringRequest(
            request.Amount, request.Currency, request.TransactionHourUtc,
            request.TransactionCountLastHour, request.RecentDeclineCount, request.IsInternational);

        var result = _engine.Score(scoringRequest);

        var fraudCase = FraudCase.Create(
            TransactionId.From(txGuid),
            CardId.From(cardGuid),
            AccountId.From(accountGuid),
            new Money(request.Amount, currency),
            result.Score,
            result.TriggeredRules.Select(r => (r.RuleName, r.Description, r.ScoreContribution)));

        _db.FraudCases.Add(fraudCase);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = fraudCase.Id.Value }, ToResponse(fraudCase));
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var fraudCase = await _db.FraudCases
            .Include(c => c.Flags)
            .FirstOrDefaultAsync(c => c.Id == FraudCaseId.From(id), ct);
        return fraudCase is null ? NotFound() : Ok(ToResponse(fraudCase));
    }

    [HttpGet("cases/card/{cardId:guid}")]
    public async Task<IActionResult> GetByCard(Guid cardId, CancellationToken ct)
    {
        var cases = await _db.FraudCases
            .Include(c => c.Flags)
            .Where(c => c.CardId == CardId.From(cardId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return Ok(cases.Select(ToResponse));
    }

    [HttpPut("cases/{id:guid}/investigate")]
    public async Task<IActionResult> StartInvestigation(Guid id, CancellationToken ct)
    {
        var fraudCase = await _db.FraudCases.FindAsync(FraudCaseId.From(id), ct);
        if (fraudCase is null) return NotFound();

        fraudCase.StartInvestigation();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(fraudCase));
    }

    [HttpPut("cases/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveFraudCaseRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<FraudResolution>(request.Resolution, true, out var resolution))
            return BadRequest(new { error = $"Invalid resolution: {request.Resolution}. Use Cleared or Confirmed." });

        var fraudCase = await _db.FraudCases.FindAsync(FraudCaseId.From(id), ct);
        if (fraudCase is null) return NotFound();

        fraudCase.Resolve(resolution, request.ResolvedBy, request.Note);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(fraudCase));
    }

    private static FraudCaseResponse ToResponse(FraudCase c) => new(
        c.Id.Value, c.TransactionId.Value, c.CardId.Value, c.AccountId.Value,
        c.Amount.Amount, c.Amount.Currency.ToString(),
        c.RiskScore, c.Status.ToString(), c.Resolution?.ToString(),
        c.ResolvedBy, c.ResolutionNote, c.ResolvedAt, c.CreatedAt,
        c.Flags.Select(f => new FraudFlagResponse(f.Id.Value, f.RuleName, f.Description, f.ScoreContribution)));
}
