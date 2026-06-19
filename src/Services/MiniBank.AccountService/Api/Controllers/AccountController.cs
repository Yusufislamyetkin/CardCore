using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.AccountService.Api.Dtos;
using MiniBank.AccountService.Domain.Entities;
using MiniBank.AccountService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/accounts")]
public sealed class AccountController : ControllerBase
{
    private readonly AccountDbContext _db;

    public AccountController(AccountDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.CustomerId, out var customerGuid))
            return BadRequest(new { error = "Invalid CustomerId" });

        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            return BadRequest(new { error = $"Invalid currency: {request.Currency}" });

        if (!Enum.TryParse<AccountType>(request.AccountType, true, out var accountType))
            return BadRequest(new { error = $"Invalid account type: {request.AccountType}" });

        var iban = Iban.Create(request.Iban);
        var account = Account.Create(CustomerId.From(customerGuid), iban, currency, accountType);

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = account.Id.Value }, ToResponse(account));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        return account is null ? NotFound() : Ok(ToResponse(account));
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId, CancellationToken ct)
    {
        var accounts = await _db.Accounts
            .Where(a => a.CustomerId == CustomerId.From(customerId))
            .ToListAsync(ct);
        return Ok(accounts.Select(ToResponse));
    }

    [HttpPost("{id:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid id, [FromBody] DepositRequest request, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        if (account is null) return NotFound();

        var amount = new Money(request.Amount, account.Balance.Currency);
        account.Deposit(amount, request.Description);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(account));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawRequest request, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        if (account is null) return NotFound();

        var amount = new Money(request.Amount, account.Balance.Currency);
        account.Withdraw(amount, request.Description);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(account));
    }

    [HttpPost("{id:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        if (account is null) return NotFound();

        account.Freeze();
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(account));
    }

    [HttpPost("{id:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        if (account is null) return NotFound();

        account.Unfreeze();
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(account));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FindAsync(AccountId.From(id), ct);
        if (account is null) return NotFound();

        account.Close();
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken ct)
    {
        var entries = await _db.LedgerEntries
            .Where(e => e.AccountId == AccountId.From(id))
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(ct);

        return Ok(entries.Select(e => new LedgerEntryResponse(
            e.Id.Value,
            e.EntryType.ToString(),
            e.Amount.Amount,
            e.Amount.Currency.ToString(),
            e.BalanceAfter.Amount,
            e.Description,
            e.TransactionRef,
            e.OccurredAt)));
    }

    private static AccountResponse ToResponse(Account a) => new(
        a.Id.Value,
        a.CustomerId.Value,
        a.Iban.Formatted,
        a.Balance.Amount,
        a.Balance.Currency.ToString(),
        a.AccountType.ToString(),
        a.Status.ToString(),
        a.CreatedAt);
}
