using Xunit;
using FluentAssertions;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.Shared.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact] public void Create_WithValidAmount_ShouldSucceed() { var m = new Money(100.50m, Currency.TRY); m.Amount.Should().Be(100.50m); m.Currency.Should().Be(Currency.TRY); }
    [Fact] public void Create_WithNegativeAmount_ShouldThrow() { var act = () => new Money(-1, Currency.TRY); act.Should().Throw<ArgumentException>(); }
    [Fact] public void Add_SameCurrency_ShouldReturnSum() { var r = new Money(100, Currency.TRY) + new Money(50, Currency.TRY); r.Amount.Should().Be(150); }
    [Fact] public void Add_DifferentCurrency_ShouldThrow() { var act = () => new Money(100, Currency.TRY) + new Money(50, Currency.USD); act.Should().Throw<CurrencyMismatchException>(); }
    [Fact] public void Subtract_SufficientFunds_ShouldWork() { var r = new Money(100, Currency.TRY) - new Money(30, Currency.TRY); r.Amount.Should().Be(70); }
    [Fact] public void Subtract_InsufficientFunds_ShouldThrow() { var act = () => new Money(30, Currency.TRY) - new Money(100, Currency.TRY); act.Should().Throw<InsufficientFundsException>(); }
    [Fact] public void Multiply_ShouldReturnProduct() { var r = new Money(100, Currency.TRY) * 1.5m; r.Amount.Should().Be(150); }
    [Fact] public void Zero_ShouldReturnZeroAmount() { Money.Zero(Currency.TRY).Amount.Should().Be(0); }
    [Fact] public void Equality_SameAmountAndCurrency_ShouldBeEqual() { new Money(100, Currency.TRY).Should().Be(new Money(100, Currency.TRY)); }
    [Fact] public void Comparison_GreaterThan_ShouldWork() { (new Money(200, Currency.TRY) > new Money(100, Currency.TRY)).Should().BeTrue(); }
    [Fact] public void Comparison_DifferentCurrency_ShouldThrow() { var act = () => new Money(100, Currency.TRY) > new Money(100, Currency.USD); act.Should().Throw<CurrencyMismatchException>(); }
}
