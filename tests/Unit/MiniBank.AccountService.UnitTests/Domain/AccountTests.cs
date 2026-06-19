using FluentAssertions;
using MiniBank.AccountService.Domain.Entities;
using MiniBank.AccountService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.AccountService.UnitTests.Domain;

public sealed class AccountTests
{
    private static readonly CustomerId TestCustomerId = CustomerId.New();
    private static readonly Iban TestIban = Iban.Create("TR330006100519786457841326");
    private static readonly Currency TestCurrency = Currency.TRY;

    private static Account CreateAccount() =>
        Account.Create(TestCustomerId, TestIban, TestCurrency, AccountType.Checking);

    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        var account = CreateAccount();

        account.Balance.Amount.Should().Be(0);
        account.Balance.Currency.Should().Be(TestCurrency);
        account.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void Create_ShouldRaiseAccountCreatedEvent()
    {
        var account = CreateAccount();

        account.DomainEvents.Should().ContainSingle(e => e is AccountCreatedEvent);
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalance()
    {
        var account = CreateAccount();
        var amount = new Money(500m, TestCurrency);

        account.Deposit(amount, "Initial deposit");

        account.Balance.Amount.Should().Be(500m);
    }

    [Fact]
    public void Deposit_ShouldRaiseMoneyDepositedEvent()
    {
        var account = CreateAccount();

        account.Deposit(new Money(100m, TestCurrency), "deposit");

        account.DomainEvents.Should().Contain(e => e is MoneyDepositedEvent);
    }

    [Fact]
    public void Withdraw_ShouldDecreaseBalance()
    {
        var account = CreateAccount();
        account.Deposit(new Money(1000m, TestCurrency), "deposit");

        account.Withdraw(new Money(300m, TestCurrency), "withdrawal");

        account.Balance.Amount.Should().Be(700m);
    }

    [Fact]
    public void Withdraw_ShouldThrow_WhenInsufficientFunds()
    {
        var account = CreateAccount();
        account.Deposit(new Money(100m, TestCurrency), "deposit");

        var act = () => account.Withdraw(new Money(200m, TestCurrency), "too much");

        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Freeze_ShouldChangeStatusToFrozen()
    {
        var account = CreateAccount();

        account.Freeze();

        account.Status.Should().Be(AccountStatus.Frozen);
    }

    [Fact]
    public void Deposit_ShouldThrow_WhenAccountFrozen()
    {
        var account = CreateAccount();
        account.Freeze();

        var act = () => account.Deposit(new Money(100m, TestCurrency), "blocked");

        act.Should().Throw<AccountFrozenException>();
    }

    [Fact]
    public void Unfreeze_ShouldRestoreActiveStatus()
    {
        var account = CreateAccount();
        account.Freeze();

        account.Unfreeze();

        account.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void Freeze_ShouldThrow_WhenAlreadyFrozen()
    {
        var account = CreateAccount();
        account.Freeze();

        var act = () => account.Freeze();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Close_ShouldChangeStatusToClosed_WhenBalanceIsZero()
    {
        var account = CreateAccount();

        account.Close();

        account.Status.Should().Be(AccountStatus.Closed);
    }

    [Fact]
    public void Close_ShouldThrow_WhenBalanceIsPositive()
    {
        var account = CreateAccount();
        account.Deposit(new Money(100m, TestCurrency), "deposit");

        var act = () => account.Close();

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
