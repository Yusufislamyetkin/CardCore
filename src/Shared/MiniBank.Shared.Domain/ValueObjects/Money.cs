using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;

namespace MiniBank.Shared.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        var result = left.Amount - right.Amount;
        if (result < 0)
            throw new InsufficientFundsException(left, right);
        return new Money(result, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative.", nameof(multiplier));
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static bool operator >(Money left, Money right) { EnsureSameCurrency(left, right); return left.Amount > right.Amount; }
    public static bool operator <(Money left, Money right) { EnsureSameCurrency(left, right); return left.Amount < right.Amount; }
    public static bool operator >=(Money left, Money right) { EnsureSameCurrency(left, right); return left.Amount >= right.Amount; }
    public static bool operator <=(Money left, Money right) { EnsureSameCurrency(left, right); return left.Amount <= right.Amount; }

    public override string ToString() => $"{Amount:N2} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new CurrencyMismatchException(left.Currency, right.Currency);
    }
}
