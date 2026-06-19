namespace MiniBank.FraudService.Api.Dtos;

public sealed record EvaluateFraudRequest(
    string TransactionId,
    string CardId,
    string AccountId,
    decimal Amount,
    string Currency,
    int TransactionHourUtc,
    int TransactionCountLastHour,
    int RecentDeclineCount,
    bool IsInternational);

public sealed record StartInvestigationRequest();

public sealed record ResolveFraudCaseRequest(
    string Resolution,
    string ResolvedBy,
    string? Note);

public sealed record FraudCaseResponse(
    Guid Id,
    Guid TransactionId,
    Guid CardId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    decimal RiskScore,
    string Status,
    string? Resolution,
    string? ResolvedBy,
    string? ResolutionNote,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    IEnumerable<FraudFlagResponse> Flags);

public sealed record FraudFlagResponse(
    Guid Id,
    string RuleName,
    string Description,
    decimal ScoreContribution);
