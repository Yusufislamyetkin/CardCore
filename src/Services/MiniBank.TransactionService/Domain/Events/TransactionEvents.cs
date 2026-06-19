using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.TransactionService.Domain.Events;

public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    Guid CardId,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    TransactionType TransactionType,
    string? AuthorizationCode) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record TransactionSettledEvent(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    Currency Currency) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record TransactionReversedEvent(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Reason) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record TransactionRefundedEvent(
    Guid TransactionId,
    Guid RefundTransactionId,
    decimal Amount,
    Currency Currency) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
