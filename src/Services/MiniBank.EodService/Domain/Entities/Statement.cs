using MiniBank.EodService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.EodService.Domain.Entities;

public sealed class Statement : AggregateRoot<StatementId>
{
    public AccountId AccountId { get; private set; } = null!;
    public EodRunId GeneratedByRunId { get; private set; } = null!;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public Money OpeningBalance { get; private set; } = null!;
    public Money ClosingBalance { get; private set; } = null!;
    public Money TotalDebits { get; private set; } = null!;
    public Money TotalCredits { get; private set; } = null!;
    public int TransactionCount { get; private set; }

    private Statement() { }

    public static Statement Generate(
        AccountId accountId,
        EodRunId runId,
        int year,
        int month,
        Money openingBalance,
        Money closingBalance,
        Money totalDebits,
        Money totalCredits,
        int transactionCount)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        var stmt = new Statement
        {
            Id = StatementId.New(),
            AccountId = accountId,
            GeneratedByRunId = runId,
            Year = year,
            Month = month,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits,
            TransactionCount = transactionCount
        };
        stmt.RaiseDomainEvent(new StatementGeneratedEvent(stmt.Id, accountId, year, month));
        return stmt;
    }
}
