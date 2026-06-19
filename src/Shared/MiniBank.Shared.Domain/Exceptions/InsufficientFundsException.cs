using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.Exceptions;

public sealed class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(Money available, Money requested)
        : base("INSUFFICIENT_FUNDS", $"Insufficient funds: available {available}, requested {requested}.") { }
}
