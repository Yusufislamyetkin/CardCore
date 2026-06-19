namespace MiniBank.NotificationService.Api.Dtos;

public sealed record QueueNotificationRequest(
    string RecipientId,
    string RecipientAddress,
    string Channel,
    string Type,
    string Subject,
    string Body);

public sealed record MarkSentRequest();
public sealed record MarkDeliveredRequest();

public sealed record MarkFailedRequest(
    string Reason);

public sealed record NotificationResponse(
    Guid Id,
    Guid RecipientId,
    string RecipientAddress,
    string Channel,
    string Type,
    string Subject,
    string Body,
    string Status,
    int RetryCount,
    string? FailureReason,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? FailedAt,
    DateTime CreatedAt,
    bool CanRetry);
