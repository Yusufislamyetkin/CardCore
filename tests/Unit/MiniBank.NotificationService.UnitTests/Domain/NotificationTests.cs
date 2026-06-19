using FluentAssertions;
using MiniBank.NotificationService.Domain.Entities;
using MiniBank.NotificationService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using Xunit;
using Notification = MiniBank.NotificationService.Domain.Entities.Notification;

namespace MiniBank.NotificationService.UnitTests.Domain;

public sealed class NotificationTests
{
    private static readonly UserId TestUserId = UserId.New();

    private static Notification CreateNotification(
        NotificationChannel channel = NotificationChannel.Email) =>
        Notification.Queue(
            TestUserId,
            "user@example.com",
            channel,
            NotificationType.TransactionAlert,
            "Transaction Alert",
            "Your card was charged 100 TRY.");

    [Fact]
    public void Queue_ShouldSetStatusPending()
    {
        var n = CreateNotification();

        n.Status.Should().Be(NotificationStatus.Pending);
        n.RetryCount.Should().Be(0);
        n.SentAt.Should().BeNull();
        n.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void Queue_ShouldRaiseNotificationQueuedEvent()
    {
        var n = CreateNotification();

        n.DomainEvents.Should().ContainSingle(e => e is NotificationQueuedEvent);
        var ev = (NotificationQueuedEvent)n.DomainEvents.First();
        ev.Channel.Should().Be("Email");
        ev.NotificationType.Should().Be("TransactionAlert");
    }

    [Fact]
    public void Queue_ShouldThrow_WhenRecipientAddressEmpty()
    {
        var act = () => Notification.Queue(
            TestUserId, "", NotificationChannel.Email,
            NotificationType.GeneralInfo, "S", "B");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Queue_ShouldThrow_WhenBodyEmpty()
    {
        var act = () => Notification.Queue(
            TestUserId, "email@test.com", NotificationChannel.Email,
            NotificationType.GeneralInfo, "Subject", "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkSent_ShouldTransitionToSent()
    {
        var n = CreateNotification();

        n.MarkSent();

        n.Status.Should().Be(NotificationStatus.Sent);
        n.SentAt.Should().NotBeNull();
        n.DomainEvents.Should().Contain(e => e is NotificationSentEvent);
    }

    [Fact]
    public void MarkSent_ShouldThrow_WhenNotPending()
    {
        var n = CreateNotification();
        n.MarkSent();

        var act = () => n.MarkSent();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void MarkDelivered_ShouldTransitionToDelivered()
    {
        var n = CreateNotification();
        n.MarkSent();

        n.MarkDelivered();

        n.Status.Should().Be(NotificationStatus.Delivered);
        n.DeliveredAt.Should().NotBeNull();
        n.DomainEvents.Should().Contain(e => e is NotificationDeliveredEvent);
    }

    [Fact]
    public void MarkDelivered_ShouldThrow_WhenNotSent()
    {
        var n = CreateNotification();

        var act = () => n.MarkDelivered();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void MarkFailed_ShouldIncrementRetryCount_AndRemainPending_WhenBelowMaxRetries()
    {
        var n = CreateNotification();

        n.MarkFailed("SMTP timeout");

        n.Status.Should().Be(NotificationStatus.Pending);
        n.RetryCount.Should().Be(1);
        n.CanRetry.Should().BeTrue();
        n.DomainEvents.OfType<NotificationFailedEvent>().Should().Contain(ev => ev.WillRetry);
    }

    [Fact]
    public void MarkFailed_ShouldTransitionToFailed_AfterMaxRetries()
    {
        var n = CreateNotification();
        n.MarkFailed("fail 1");
        n.MarkFailed("fail 2");

        n.MarkFailed("fail 3");

        n.Status.Should().Be(NotificationStatus.Failed);
        n.RetryCount.Should().Be(3);
        n.FailedAt.Should().NotBeNull();
        n.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void MarkFailed_ShouldRaiseFailedEvent_WithWillRetryFalse_WhenMaxReached()
    {
        var n = CreateNotification();
        n.MarkFailed("fail 1");
        n.MarkFailed("fail 2");
        n.MarkFailed("fail 3");

        var lastEvent = (NotificationFailedEvent)n.DomainEvents
            .Where(e => e is NotificationFailedEvent)
            .Last();

        lastEvent.WillRetry.Should().BeFalse();
        lastEvent.FailureReason.Should().Be("fail 3");
    }

    [Fact]
    public void MarkFailed_ShouldThrow_WhenAlreadyDelivered()
    {
        var n = CreateNotification();
        n.MarkSent();
        n.MarkDelivered();

        var act = () => n.MarkFailed("late failure");

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
