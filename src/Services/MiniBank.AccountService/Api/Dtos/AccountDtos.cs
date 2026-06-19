namespace MiniBank.AccountService.Api.Dtos;

public sealed record CreateAccountRequest(
    string CustomerId,
    string Iban,
    string Currency,
    string AccountType);

public sealed record DepositRequest(decimal Amount, string Description);

public sealed record WithdrawRequest(decimal Amount, string Description);

public sealed record AccountResponse(
    Guid Id,
    Guid CustomerId,
    string Iban,
    decimal Balance,
    string Currency,
    string AccountType,
    string Status,
    DateTime CreatedAt);

public sealed record LedgerEntryResponse(
    Guid Id,
    string EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    string? TransactionRef,
    DateTime OccurredAt);
