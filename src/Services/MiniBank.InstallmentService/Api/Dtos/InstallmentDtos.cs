namespace MiniBank.InstallmentService.Api.Dtos;

public sealed record CreateInstallmentPlanRequest(
    string CardId,
    string AccountId,
    string TransactionId,
    decimal PrincipalAmount,
    string Currency,
    int InstallmentCount,
    decimal AnnualInterestRate,
    DateTime FirstDueDate);

public sealed record PayInstallmentRequest(int InstallmentNumber);

public sealed record CancelPlanRequest(string Reason);

public sealed record InstallmentPlanResponse(
    Guid Id,
    Guid CardId,
    Guid AccountId,
    Guid TransactionId,
    decimal TotalAmount,
    decimal InstallmentAmount,
    string Currency,
    int InstallmentCount,
    int PaidCount,
    decimal InterestRate,
    string Status,
    DateTime CreatedAt,
    IEnumerable<InstallmentItemResponse> Items);

public sealed record InstallmentItemResponse(
    Guid Id,
    int InstallmentNumber,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    DateTime? PaidAt,
    string Status);
