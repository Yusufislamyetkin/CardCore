using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.NotificationService.Domain.Events;

public sealed record NotificationQueuedEvent(
    NotificationId NotificationId,
    string Channel,
    string NotificationType,
    string RecipientAddress) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record NotificationSentEvent(
    NotificationId NotificationId,
    DateTime SentAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record NotificationDeliveredEvent(
    NotificationId NotificationId,
    DateTime DeliveredAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record NotificationFailedEvent(
    NotificationId NotificationId,
    string FailureReason,
    bool WillRetry) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
