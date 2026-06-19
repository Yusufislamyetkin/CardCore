using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.TransactionService.Api.Dtos;
using MiniBank.TransactionService.Domain.Entities;
using MiniBank.TransactionService.Infrastructure.Persistence;

namespace MiniBank.TransactionService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/transactions")]
public sealed class TransactionController : ControllerBase
{
    private readonly TransactionDbContext _db;

    public TransactionController(TransactionDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.CardId, out var cardGuid) ||
            !Guid.TryParse(request.AccountId, out var accountGuid))
            return BadRequest(new { error = "Invalid CardId or AccountId" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        if (!Enum.TryParse<TransactionType>(request.TransactionType, true, out var txType))
            return BadRequest(new { error = $"Invalid transaction type: {request.TransactionType}" });

        var transaction = Transaction.Create(
            CardId.From(cardGuid), AccountId.From(accountGuid),
            new Money(request.Amount, currency), txType,
            request.MerchantId, request.MerchantName,
            request.AuthorizationId, request.AuthorizationCode);

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = transaction.Id.Value }, ToResponse(transaction));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FindAsync(TransactionId.From(id), ct);
        return tx is null ? NotFound() : Ok(ToResponse(tx));
    }

    [HttpGet("card/{cardId:guid}")]
    public async Task<IActionResult> GetByCard(Guid cardId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 20;
        var txs = await _db.Transactions
            .Where(t => t.CardId == CardId.From(cardId))
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return Ok(txs.Select(ToResponse));
    }

    [HttpPost("{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FindAsync(TransactionId.From(id), ct);
        if (tx is null) return NotFound();

        tx.Settle();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(tx));
    }

    [HttpPost("{id:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseRequest request, CancellationToken ct)
    {
        var tx = await _db.Transactions.FindAsync(TransactionId.From(id), ct);
        if (tx is null) return NotFound();

        tx.Reverse(request.Reason);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(tx));
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest request, CancellationToken ct)
    {
        var tx = await _db.Transactions.FindAsync(TransactionId.From(id), ct);
        if (tx is null) return NotFound();

        var refundTx = tx.Refund(new Money(request.Amount, tx.Amount.Currency));
        _db.Transactions.Update(tx);
        _db.Transactions.Add(refundTx);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(refundTx));
    }

    private static TransactionResponse ToResponse(Transaction t) => new(
        t.Id.Value, t.CardId.Value, t.AccountId.Value,
        t.Amount.Amount, t.Amount.Currency.ToString(),
        t.TransactionType.ToString(), t.Status.ToString(),
        t.MerchantId, t.MerchantName,
        t.AuthorizationCode, t.ReferenceId,
        t.CreatedAt, t.SettledAt);
}
