using MiniBank.Shared.Domain.Enums;

namespace MiniBank.Shared.Domain.Exceptions;

public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(Currency expected, Currency actual)
        : base("CURRENCY_MISMATCH", $"Currency mismatch: expected {expected}, got {actual}.") { }
}
