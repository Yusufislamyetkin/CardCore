using FluentAssertions;
using MiniBank.CardService.Domain.Entities;
using MiniBank.CardService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.CardService.UnitTests.Domain;

public sealed class CardTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Visa test PAN — passes Luhn
    private static readonly Pan TestPan = Pan.Create("4532015112830366");
    private static readonly CardExpiry TestExpiry = CardExpiry.Create(12, 27);
    private static readonly AccountId TestAccountId = AccountId.New();
    private static readonly CustomerId TestCustomerId = CustomerId.New();

    private static Card IssueCard() =>
        Card.Issue(TestAccountId, TestCustomerId, TestPan, TestExpiry, CardType.Debit);

    [Fact]
    public void Issue_ShouldCreateCardInCreatedStatus()
    {
        var card = IssueCard();

        card.Status.Should().Be(CardStatus.Created);
        card.PanMasked.Should().Contain("*");
        card.Last4.Should().Be("0366");
    }

    [Fact]
    public void Issue_ShouldRaiseCardIssuedEvent()
    {
        var card = IssueCard();

        card.DomainEvents.Should().ContainSingle(e => e is CardIssuedEvent);
    }

    [Fact]
    public void Issue_ShouldNeverStoreRawPan()
    {
        var card = IssueCard();

        // Raw PAN should never appear in any string property
        card.PanMasked.Should().NotBe("4532015112830366");
        card.PanHash.Should().NotBe("4532015112830366");
    }

    [Fact]
    public void Activate_ShouldChangeStatusToActive()
    {
        var card = IssueCard();

        card.Activate(UtcNow);

        card.Status.Should().Be(CardStatus.Active);
    }

    [Fact]
    public void Activate_ShouldThrow_WhenCardIsExpired()
    {
        var expiredCard = Card.Issue(TestAccountId, TestCustomerId, TestPan,
            CardExpiry.Create(1, 20), CardType.Debit);

        var act = () => expiredCard.Activate(UtcNow);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Block_ShouldChangeStatusToBlocked()
    {
        var card = IssueCard();
        card.Activate(UtcNow);

        card.Block("Suspicious activity");

        card.Status.Should().Be(CardStatus.Blocked);
        card.BlockReason.Should().Be("Suspicious activity");
    }

    [Fact]
    public void Unblock_ShouldRestoreActiveStatus()
    {
        var card = IssueCard();
        card.Activate(UtcNow);
        card.Block("Test block");

        card.Unblock();

        card.Status.Should().Be(CardStatus.Active);
        card.BlockReason.Should().BeNull();
    }

    [Fact]
    public void ReportStolenOrLost_ShouldChangStatusToStolenLost()
    {
        var card = IssueCard();
        card.Activate(UtcNow);

        card.ReportStolenOrLost();

        card.Status.Should().Be(CardStatus.StolenLost);
    }

    [Fact]
    public void Unblock_ShouldThrow_WhenCardIsStolenLost()
    {
        var card = IssueCard();
        card.Activate(UtcNow);
        card.ReportStolenOrLost();

        var act = () => card.Unblock();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Close_ShouldChangeStatusToClosed()
    {
        var card = IssueCard();
        card.Activate(UtcNow);

        card.Close();

        card.Status.Should().Be(CardStatus.Closed);
    }

    [Fact]
    public void SetLimit_ShouldAddNewLimit()
    {
        var card = IssueCard();
        var limit = new Money(1000m, Currency.TRY);

        card.SetLimit(LimitType.Daily, limit);

        card.Limits.Should().ContainSingle(l => l.LimitType == LimitType.Daily);
        card.Limits.First().Amount.Amount.Should().Be(1000m);
    }

    [Fact]
    public void SetLimit_ShouldUpdateExistingLimit()
    {
        var card = IssueCard();
        card.SetLimit(LimitType.Daily, new Money(1000m, Currency.TRY));

        card.SetLimit(LimitType.Daily, new Money(2000m, Currency.TRY));

        card.Limits.Should().ContainSingle(l => l.LimitType == LimitType.Daily);
        card.Limits.First().Amount.Amount.Should().Be(2000m);
    }

    [Fact]
    public void IsUsable_ShouldReturnTrue_WhenActiveAndNotExpired()
    {
        var card = IssueCard();
        card.Activate(UtcNow);

        card.IsUsable(UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsUsable_ShouldReturnFalse_WhenBlocked()
    {
        var card = IssueCard();
        card.Activate(UtcNow);
        card.Block("block");

        card.IsUsable(UtcNow).Should().BeFalse();
    }
}
