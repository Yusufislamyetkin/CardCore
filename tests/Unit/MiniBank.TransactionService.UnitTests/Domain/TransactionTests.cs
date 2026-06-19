using FluentAssertions;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.TransactionService.Domain.Entities;
using MiniBank.TransactionService.Domain.Events;
using Xunit;

namespace MiniBank.TransactionService.UnitTests.Domain;

public sealed class TransactionTests
{
    private static readonly CardId TestCardId = CardId.New();
    private static readonly AccountId TestAccountId = AccountId.New();
    private static readonly Money TestAmount = new(500m, Currency.TRY);

    private static Transaction CreateTransaction() =>
        Transaction.Create(TestCardId, TestAccountId, TestAmount,
            TransactionType.Purchase, "MERCH001", "Test Store", "AUTH001", "123456");

    [Fact]
    public void Create_ShouldInitializeWithApprovedStatus()
    {
        var tx = CreateTransaction();

        tx.Status.Should().Be(TransactionStatus.Approved);
        tx.AuthorizationCode.Should().Be("123456");
        tx.SettledAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseTransactionCreatedEvent()
    {
        var tx = CreateTransaction();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionCreatedEvent);
    }

    [Fact]
    public void Settle_ShouldChangeStatusToSettled()
    {
        var tx = CreateTransaction();

        tx.Settle();

        tx.Status.Should().Be(TransactionStatus.Settled);
        tx.SettledAt.Should().NotBeNull();
    }

    [Fact]
    public void Settle_ShouldRaiseTransactionSettledEvent()
    {
        var tx = CreateTransaction();

        tx.Settle();

        tx.DomainEvents.Should().Contain(e => e is TransactionSettledEvent);
    }

    [Fact]
    public void Settle_ShouldThrow_WhenAlreadySettled()
    {
        var tx = CreateTransaction();
        tx.Settle();

        var act = () => tx.Settle();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Reverse_ShouldChangeStatusToReversed()
    {
        var tx = CreateTransaction();

        tx.Reverse("Customer cancellation");

        tx.Status.Should().Be(TransactionStatus.Reversed);
        tx.ReverseReason.Should().Be("Customer cancellation");
    }

    [Fact]
    public void Reverse_ShouldThrow_WhenAlreadyReversed()
    {
        var tx = CreateTransaction();
        tx.Reverse("First reversal");

        var act = () => tx.Reverse("Second reversal");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Refund_ShouldReturnRefundTransaction()
    {
        var tx = CreateTransaction();
        tx.Settle();

        var refund = tx.Refund(new Money(200m, Currency.TRY));

        refund.Should().NotBeNull();
        refund.TransactionType.Should().Be(TransactionType.Refund);
        refund.Amount.Amount.Should().Be(200m);
        tx.Status.Should().Be(TransactionStatus.Refunded);
    }

    [Fact]
    public void Refund_ShouldThrow_WhenNotSettled()
    {
        var tx = CreateTransaction();

        var act = () => tx.Refund(new Money(100m, Currency.TRY));

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Refund_ShouldThrow_WhenRefundExceedsOriginal()
    {
        var tx = CreateTransaction();
        tx.Settle();

        var act = () => tx.Refund(new Money(600m, Currency.TRY));

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
