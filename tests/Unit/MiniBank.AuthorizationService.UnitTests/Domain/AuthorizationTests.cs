using FluentAssertions;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.AuthorizationService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.AuthorizationService.UnitTests.Domain;

public sealed class AuthorizationTests
{
    private static readonly CardId TestCardId = CardId.New();
    private static readonly AccountId TestAccountId = AccountId.New();
    private const string TestPanHash = "abc123hash";
    private static readonly Money TestAmount = new(100m, Currency.TRY);

    [Fact]
    public void Approve_ShouldCreateApprovedAuthorization()
    {
        var auth = Authorization.Approve(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant");

        auth.Status.Should().Be(AuthorizationStatus.Approved);
        auth.AuthorizationCode.Should().NotBeNullOrWhiteSpace();
        auth.DeclineReason.Should().Be(DeclineReason.None);
    }

    [Fact]
    public void Approve_ShouldRaiseAuthorizationApprovedEvent()
    {
        var auth = Authorization.Approve(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant");

        auth.DomainEvents.Should().ContainSingle(e => e is AuthorizationApprovedEvent);
    }

    [Fact]
    public void Decline_ShouldCreateDeclinedAuthorization()
    {
        var auth = Authorization.Decline(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant", DeclineReason.InsufficientFunds);

        auth.Status.Should().Be(AuthorizationStatus.Declined);
        auth.AuthorizationCode.Should().BeNull();
        auth.DeclineReason.Should().Be(DeclineReason.InsufficientFunds);
    }

    [Fact]
    public void Decline_ShouldRaiseAuthorizationDeclinedEvent()
    {
        var auth = Authorization.Decline(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant", DeclineReason.CardNotActive);

        auth.DomainEvents.Should().ContainSingle(e => e is AuthorizationDeclinedEvent);
    }

    [Fact]
    public void Reverse_ShouldChangeStatusToReversed()
    {
        var auth = Authorization.Approve(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant");

        auth.Reverse("Customer request");

        auth.Status.Should().Be(AuthorizationStatus.Reversed);
        auth.ReverseReason.Should().Be("Customer request");
    }

    [Fact]
    public void Reverse_ShouldRaiseAuthorizationReversedEvent()
    {
        var auth = Authorization.Approve(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant");

        auth.Reverse("Customer request");

        auth.DomainEvents.Should().Contain(e => e is AuthorizationReversedEvent);
    }

    [Fact]
    public void Reverse_ShouldThrow_WhenAlreadyDeclined()
    {
        var auth = Authorization.Decline(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant", DeclineReason.InsufficientFunds);

        var act = () => auth.Reverse("Should fail");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Reverse_ShouldThrow_WhenAlreadyReversed()
    {
        var auth = Authorization.Approve(TestCardId, TestAccountId, TestPanHash,
            TestAmount, "MERCH001", "Test Merchant");
        auth.Reverse("First reversal");

        var act = () => auth.Reverse("Second reversal");

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
