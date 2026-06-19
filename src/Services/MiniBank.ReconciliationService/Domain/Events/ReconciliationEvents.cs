using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.ReconciliationService.Domain.Events;

public sealed record ReconciliationBatchCreatedEvent(
    ReconciliationBatchId BatchId,
    DateOnly ReconciliationDate,
    string NetworkSource) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record ReconciliationBatchCompletedEvent(
    ReconciliationBatchId BatchId,
    int MatchedCount,
    int UnmatchedCount,
    int DisputedCount) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record ReconciliationDiscrepancyFoundEvent(
    ReconciliationBatchId BatchId,
    ReconciliationItemId ItemId,
    string ExternalTransactionId,
    decimal DiscrepancyAmount) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
