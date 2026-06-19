using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.CardService.Api.Dtos;
using MiniBank.CardService.Domain.Entities;
using MiniBank.CardService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.CardService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/cards")]
public sealed class CardController : ControllerBase
{
    private readonly CardDbContext _db;

    public CardController(CardDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Issue([FromBody] IssueCardRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.AccountId, out var accountGuid))
            return BadRequest(new { error = "Invalid AccountId" });
        if (!Guid.TryParse(request.CustomerId, out var customerGuid))
            return BadRequest(new { error = "Invalid CustomerId" });
        if (!Enum.TryParse<CardType>(request.CardType, true, out var cardType))
            return BadRequest(new { error = $"Invalid card type: {request.CardType}" });

        var pan = Pan.Create(request.Pan);
        var expiry = CardExpiry.Parse(request.Expiry);
        var card = Card.Issue(AccountId.From(accountGuid), CustomerId.From(customerGuid), pan, expiry, cardType);

        _db.Cards.Add(card);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = card.Id.Value }, ToResponse(card));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards
            .Include(c => c.Limits)
            .FirstOrDefaultAsync(c => c.Id == CardId.From(id), ct);
        return card is null ? NotFound() : Ok(ToResponse(card));
    }

    [HttpGet("account/{accountId:guid}")]
    public async Task<IActionResult> GetByAccount(Guid accountId, CancellationToken ct)
    {
        var cards = await _db.Cards
            .Where(c => c.AccountId == AccountId.From(accountId))
            .ToListAsync(ct);
        return Ok(cards.Select(ToResponse));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards.FindAsync(CardId.From(id), ct);
        if (card is null) return NotFound();

        card.Activate(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(card));
    }

    [HttpPost("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, [FromBody] string reason, CancellationToken ct)
    {
        var card = await _db.Cards.FindAsync(CardId.From(id), ct);
        if (card is null) return NotFound();

        card.Block(reason);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(card));
    }

    [HttpPost("{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards.FindAsync(CardId.From(id), ct);
        if (card is null) return NotFound();

        card.Unblock();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(card));
    }

    [HttpPost("{id:guid}/stolen-lost")]
    public async Task<IActionResult> StolenLost(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards.FindAsync(CardId.From(id), ct);
        if (card is null) return NotFound();

        card.ReportStolenOrLost();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(card));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var card = await _db.Cards.FindAsync(CardId.From(id), ct);
        if (card is null) return NotFound();

        card.Close();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/limits/{limitType}")]
    public async Task<IActionResult> SetLimit(Guid id, string limitType, [FromBody] SetLimitRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<LimitType>(limitType, true, out var limitTypeEnum))
            return BadRequest(new { error = $"Invalid limit type: {limitType}" });
        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        var card = await _db.Cards
            .Include(c => c.Limits)
            .FirstOrDefaultAsync(c => c.Id == CardId.From(id), ct);
        if (card is null) return NotFound();

        card.SetLimit(limitTypeEnum, new Money(request.Amount, currency));
        await _db.SaveChangesAsync(ct);

        return Ok(card.Limits.Select(l => new CardLimitResponse(
            l.LimitType.ToString(), l.Amount.Amount, l.Amount.Currency.ToString())));
    }

    private static CardResponse ToResponse(Card c) => new(
        c.Id.Value,
        c.AccountId.Value,
        c.CustomerId.Value,
        c.PanMasked,
        c.Last4,
        c.Bin,
        c.Expiry.ToString(),
        c.CardType.ToString(),
        c.Status.ToString(),
        c.BlockReason,
        c.CreatedAt);
}
