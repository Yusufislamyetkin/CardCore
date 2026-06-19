namespace MiniBank.ReconciliationService.Api.Dtos;

public sealed record CreateBatchRequest(
    DateOnly ReconciliationDate,
    string NetworkSource);

public sealed record AddItemRequest(
    string ExternalTransactionId,
    decimal ExternalAmount,
    string Currency);

public sealed record MatchItemRequest(
    string ExternalTransactionId,
    Guid InternalTransactionId,
    decimal InternalAmount);

public sealed record MarkUnmatchedRequest(
    string ExternalTransactionId);

public sealed record ResolveItemRequest(
    string ExternalTransactionId,
    string Note);

public sealed record FailBatchRequest(
    string Reason);

public sealed record ReconciliationBatchResponse(
    Guid Id,
    DateOnly ReconciliationDate,
    string NetworkSource,
    string Status,
    int ItemCount,
    int MatchedCount,
    int UnmatchedCount,
    int DisputedCount,
    DateTime? CompletedAt,
    string? FailureReason,
    DateTime CreatedAt,
    IEnumerable<ReconciliationItemResponse> Items);

public sealed record ReconciliationItemResponse(
    Guid Id,
    string ExternalTransactionId,
    Guid? InternalTransactionId,
    decimal ExternalAmount,
    decimal InternalAmount,
    string Currency,
    decimal DiscrepancyAmount,
    string Status,
    string? ResolutionNote);
