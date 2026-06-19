namespace MiniBank.TransactionService.Api.Dtos;

public sealed record CreateTransactionRequest(
    string CardId,
    string AccountId,
    decimal Amount,
    string Currency,
    string TransactionType,
    string MerchantId,
    string MerchantName,
    string? AuthorizationId = null,
    string? AuthorizationCode = null);

public sealed record RefundRequest(decimal Amount);

public sealed record ReverseRequest(string Reason);

public sealed record TransactionResponse(
    Guid Id,
    Guid CardId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string TransactionType,
    string Status,
    string MerchantId,
    string MerchantName,
    string? AuthorizationCode,
    string? ReferenceId,
    DateTime CreatedAt,
    DateTime? SettledAt);
