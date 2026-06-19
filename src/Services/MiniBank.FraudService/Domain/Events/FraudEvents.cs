using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.FraudService.Domain.Events;

public sealed record FraudCaseCreatedEvent(
    FraudCaseId CaseId,
    TransactionId TransactionId,
    CardId CardId,
    decimal RiskScore) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record FraudCaseInvestigatingEvent(
    FraudCaseId CaseId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record FraudCaseResolvedEvent(
    FraudCaseId CaseId,
    string Resolution,
    string ResolvedBy) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
