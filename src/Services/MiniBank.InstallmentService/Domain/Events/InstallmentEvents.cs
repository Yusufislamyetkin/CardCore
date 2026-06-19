using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.InstallmentService.Domain.Events;

public sealed record InstallmentPlanCreatedEvent(
    Guid PlanId,
    Guid CardId,
    Guid AccountId,
    Guid TransactionId,
    decimal TotalAmount,
    Currency Currency,
    int InstallmentCount) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record InstallmentPaidEvent(
    Guid PlanId,
    int InstallmentNumber,
    decimal Amount,
    Currency Currency) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record InstallmentPlanCompletedEvent(Guid PlanId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record InstallmentPlanCancelledEvent(Guid PlanId, string Reason) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
