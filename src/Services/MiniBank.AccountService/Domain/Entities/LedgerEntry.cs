using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Domain.Entities;

public sealed class LedgerEntry
{
    public LedgerEntryId Id { get; private set; } = default!;
    public AccountId AccountId { get; private set; } = default!;
    public EntryType EntryType { get; private set; }
    public Money Amount { get; private set; } = default!;
    public Money BalanceAfter { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public string? TransactionRef { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private LedgerEntry() { }

    public static LedgerEntry Create(
        AccountId accountId,
        EntryType entryType,
        Money amount,
        Money balanceAfter,
        string description,
        string? transactionRef = null) =>
        new()
        {
            Id = LedgerEntryId.New(),
            AccountId = accountId,
            EntryType = entryType,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Description = description,
            TransactionRef = transactionRef,
            OccurredAt = DateTime.UtcNow
        };
}
