namespace MiniBank.AuthorizationService.Api.Dtos;

public sealed record AuthorizeRequest(
    string CardId,
    string AccountId,
    string PanHash,
    decimal Amount,
    string Currency,
    string MerchantId,
    string MerchantName,
    string CardStatus,
    string CardExpiry,
    decimal AvailableBalance,
    decimal? DailyLimit,
    decimal? MonthlyLimit);

public sealed record AuthorizationResponse(
    Guid AuthorizationId,
    bool Approved,
    string? AuthorizationCode,
    string? DeclineReason,
    decimal Amount,
    string Currency,
    DateTime CreatedAt);

public sealed record ReverseRequest(string Reason);
