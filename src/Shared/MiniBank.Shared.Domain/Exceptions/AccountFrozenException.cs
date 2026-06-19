namespace MiniBank.Shared.Domain.Exceptions;

public sealed class AccountFrozenException : DomainException
{
    public AccountFrozenException(string accountId)
        : base("ACCOUNT_FROZEN", $"Account '{accountId}' is frozen. No operations allowed.") { }
}
