using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.AccountService.Domain.Events;

public sealed record AccountCreatedEvent(
    Guid AccountId,
    Guid CustomerId,
    string Iban,
    Currency Currency,
    AccountType AccountType) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record MoneyDepositedEvent(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    decimal BalanceAfter) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record MoneyWithdrawnEvent(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    decimal BalanceAfter) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record AccountFrozenEvent(Guid AccountId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record AccountUnfrozenEvent(Guid AccountId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record AccountClosedEvent(Guid AccountId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
