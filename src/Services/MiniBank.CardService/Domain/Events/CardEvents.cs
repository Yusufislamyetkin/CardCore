using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.CardService.Domain.Events;

public sealed record CardIssuedEvent(
    Guid CardId,
    Guid AccountId,
    Guid CustomerId,
    string PanMasked,
    CardType CardType) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardActivatedEvent(Guid CardId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardBlockedEvent(Guid CardId, string Reason) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardUnblockedEvent(Guid CardId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardStolenLostEvent(Guid CardId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardClosedEvent(Guid CardId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record CardLimitUpdatedEvent(
    Guid CardId,
    LimitType LimitType,
    decimal Amount,
    Currency Currency) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
