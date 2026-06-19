using MiniBank.NotificationService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.NotificationService.Domain.Entities;

public enum NotificationChannel { Email = 1, Sms = 2, Push = 3 }
public enum NotificationStatus { Pending = 1, Sent = 2, Delivered = 3, Failed = 4 }

public enum NotificationType
{
    TransactionAlert = 1,
    LoginAlert = 2,
    FraudAlert = 3,
    StatementReady = 4,
    CardBlocked = 5,
    PasswordChanged = 6,
    LimitExceeded = 7,
    GeneralInfo = 8
}

public sealed class Notification : AggregateRoot<NotificationId>
{
    private const int MaxRetries = 3;

    public UserId RecipientId { get; private set; } = null!;
    public string RecipientAddress { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public NotificationType Type { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? FailedAt { get; private set; }

    private Notification() { }

    public static Notification Queue(
        UserId recipientId,
        string recipientAddress,
        NotificationChannel channel,
        NotificationType type,
        string subject,
        string body)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
            throw new ArgumentException("Recipient address is required", nameof(recipientAddress));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));

        var notification = new Notification
        {
            Id = NotificationId.New(),
            RecipientId = recipientId,
            RecipientAddress = recipientAddress,
            Channel = channel,
            Type = type,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Pending,
            RetryCount = 0
        };
        notification.RaiseDomainEvent(new NotificationQueuedEvent(
            notification.Id, channel.ToString(), type.ToString(), recipientAddress));
        return notification;
    }

    public void MarkSent()
    {
        if (Status != NotificationStatus.Pending)
            throw new InvalidStateTransitionException("Notification", Status.ToString(), NotificationStatus.Sent.ToString());

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new NotificationSentEvent(Id, SentAt.Value));
    }

    public void MarkDelivered()
    {
        if (Status != NotificationStatus.Sent)
            throw new InvalidStateTransitionException("Notification", Status.ToString(), NotificationStatus.Delivered.ToString());

        Status = NotificationStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new NotificationDeliveredEvent(Id, DeliveredAt.Value));
    }

    public void MarkFailed(string reason)
    {
        if (Status != NotificationStatus.Pending && Status != NotificationStatus.Sent)
            throw new InvalidStateTransitionException("Notification", Status.ToString(), NotificationStatus.Failed.ToString());

        RetryCount++;
        FailureReason = reason;

        var willRetry = RetryCount < MaxRetries;
        if (!willRetry)
        {
            Status = NotificationStatus.Failed;
            FailedAt = DateTime.UtcNow;
        }
        else
        {
            Status = NotificationStatus.Pending;
        }

        MarkModified();
        RaiseDomainEvent(new NotificationFailedEvent(Id, reason, willRetry));
    }

    public bool CanRetry => Status == NotificationStatus.Pending && RetryCount < MaxRetries;
}
