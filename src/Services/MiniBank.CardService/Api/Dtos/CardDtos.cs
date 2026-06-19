namespace MiniBank.CardService.Api.Dtos;

public sealed record IssueCardRequest(
    string AccountId,
    string CustomerId,
    string Pan,
    string Expiry,
    string CardType);

public sealed record SetLimitRequest(decimal Amount, string Currency);

public sealed record CardResponse(
    Guid Id,
    Guid AccountId,
    Guid CustomerId,
    string PanMasked,
    string Last4,
    string Bin,
    string Expiry,
    string CardType,
    string Status,
    string? BlockReason,
    DateTime CreatedAt);

public sealed record CardLimitResponse(
    string LimitType,
    decimal Amount,
    string Currency);
