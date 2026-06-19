namespace MiniBank.EodService.Api.Dtos;

public sealed record ScheduleEodRunRequest(
    DateOnly ProcessingDate);

public sealed record StartEodRunRequest();

public sealed record CompleteEodRunRequest(
    int ProcessedTransactionCount,
    decimal TotalSettledAmount);

public sealed record FailEodRunRequest(
    string ErrorMessage);

public sealed record GenerateStatementRequest(
    string AccountId,
    int Year,
    int Month,
    decimal OpeningBalanceAmount,
    string Currency,
    decimal ClosingBalanceAmount,
    decimal TotalDebitsAmount,
    decimal TotalCreditsAmount,
    int TransactionCount);

public sealed record EodRunResponse(
    Guid Id,
    DateOnly ProcessingDate,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int ProcessedTransactionCount,
    decimal TotalSettledAmount,
    string? ErrorMessage,
    DateTime CreatedAt);

public sealed record StatementResponse(
    Guid Id,
    Guid AccountId,
    Guid GeneratedByRunId,
    int Year,
    int Month,
    decimal OpeningBalanceAmount,
    decimal ClosingBalanceAmount,
    decimal TotalDebitsAmount,
    decimal TotalCreditsAmount,
    string Currency,
    int TransactionCount,
    DateTime CreatedAt);
