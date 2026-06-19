using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.InstallmentService.Api.Dtos;
using MiniBank.InstallmentService.Domain.Entities;
using MiniBank.InstallmentService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.InstallmentService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/installments")]
public sealed class InstallmentController : ControllerBase
{
    private readonly InstallmentDbContext _db;

    public InstallmentController(InstallmentDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallmentPlanRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.CardId, out var cardGuid) ||
            !Guid.TryParse(request.AccountId, out var accountGuid) ||
            !Guid.TryParse(request.TransactionId, out var txGuid))
            return BadRequest(new { error = "Invalid CardId, AccountId, or TransactionId" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        if (await _db.InstallmentPlans.AnyAsync(p => p.TransactionId == TransactionId.From(txGuid), ct))
            return Conflict(new { error = "Installment plan already exists for this transaction" });

        var plan = InstallmentPlan.Create(
            CardId.From(cardGuid), AccountId.From(accountGuid), TransactionId.From(txGuid),
            new Money(request.PrincipalAmount, currency),
            request.InstallmentCount, request.AnnualInterestRate, request.FirstDueDate);

        _db.InstallmentPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = plan.Id.Value }, ToResponse(plan));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var plan = await _db.InstallmentPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == InstallmentPlanId.From(id), ct);
        return plan is null ? NotFound() : Ok(ToResponse(plan));
    }

    [HttpGet("card/{cardId:guid}")]
    public async Task<IActionResult> GetByCard(Guid cardId, CancellationToken ct)
    {
        var plans = await _db.InstallmentPlans
            .Include(p => p.Items)
            .Where(p => p.CardId == CardId.From(cardId))
            .ToListAsync(ct);
        return Ok(plans.Select(ToResponse));
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayInstallmentRequest request, CancellationToken ct)
    {
        var plan = await _db.InstallmentPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == InstallmentPlanId.From(id), ct);
        if (plan is null) return NotFound();

        plan.PayInstallment(request.InstallmentNumber, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(plan));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelPlanRequest request, CancellationToken ct)
    {
        var plan = await _db.InstallmentPlans.FindAsync(InstallmentPlanId.From(id), ct);
        if (plan is null) return NotFound();

        plan.Cancel(request.Reason);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Plan cancelled", reason = request.Reason });
    }

    private static InstallmentPlanResponse ToResponse(InstallmentPlan p) => new(
        p.Id.Value, p.CardId.Value, p.AccountId.Value, p.TransactionId.Value,
        p.TotalAmount.Amount, p.InstallmentAmount.Amount, p.TotalAmount.Currency.ToString(),
        p.InstallmentCount, p.PaidCount, p.InterestRate, p.Status.ToString(), p.CreatedAt,
        p.Items.OrderBy(i => i.InstallmentNumber).Select(i => new InstallmentItemResponse(
            i.Id, i.InstallmentNumber, i.Amount.Amount, i.Amount.Currency.ToString(),
            i.DueDate, i.PaidAt, i.Status.ToString())));
}
