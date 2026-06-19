using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.AuthorizationService.Domain.Events;

public sealed record AuthorizationApprovedEvent(
    Guid AuthorizationId,
    Guid CardId,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string AuthorizationCode) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record AuthorizationDeclinedEvent(
    Guid AuthorizationId,
    Guid CardId,
    decimal Amount,
    Currency Currency,
    DeclineReason Reason) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record AuthorizationReversedEvent(
    Guid AuthorizationId,
    Guid CardId,
    decimal Amount,
    Currency Currency,
    string Reason) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
