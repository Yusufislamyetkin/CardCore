using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.EodService.Domain.Events;

public sealed record EodRunScheduledEvent(
    EodRunId RunId,
    DateOnly ProcessingDate) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record EodRunStartedEvent(
    EodRunId RunId,
    DateOnly ProcessingDate,
    DateTime StartedAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record EodRunCompletedEvent(
    EodRunId RunId,
    DateOnly ProcessingDate,
    int ProcessedTransactionCount,
    decimal TotalSettledAmount) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record EodRunFailedEvent(
    EodRunId RunId,
    DateOnly ProcessingDate,
    string ErrorMessage) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record StatementGeneratedEvent(
    StatementId StatementId,
    AccountId AccountId,
    int Year,
    int Month) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
