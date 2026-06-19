using FluentAssertions;
using MiniBank.InstallmentService.Domain.Entities;
using MiniBank.InstallmentService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.InstallmentService.UnitTests.Domain;

public sealed class InstallmentPlanTests
{
    private static readonly CardId TestCardId = CardId.New();
    private static readonly AccountId TestAccountId = AccountId.New();
    private static readonly TransactionId TestTxId = TransactionId.New();
    private static readonly DateTime FirstDue = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private static InstallmentPlan CreatePlan(int count = 3, decimal rate = 0) =>
        InstallmentPlan.Create(TestCardId, TestAccountId, TestTxId,
            new Money(300m, Currency.TRY), count, rate, FirstDue);

    [Fact]
    public void Create_ShouldGenerateCorrectNumberOfInstallments()
    {
        var plan = CreatePlan(3);

        plan.Items.Count.Should().Be(3);
        plan.InstallmentCount.Should().Be(3);
    }

    [Fact]
    public void Create_ZeroInterest_ShouldDivideEquallyAcrossInstallments()
    {
        var plan = CreatePlan(3, rate: 0);

        plan.InstallmentAmount.Amount.Should().Be(100m);
        plan.TotalAmount.Amount.Should().Be(300m);
    }

    [Fact]
    public void Create_ShouldAssignCorrectDueDates()
    {
        var plan = CreatePlan(3);

        plan.Items.First(i => i.InstallmentNumber == 1).DueDate.Should().Be(FirstDue);
        plan.Items.First(i => i.InstallmentNumber == 2).DueDate.Should().Be(FirstDue.AddMonths(1));
        plan.Items.First(i => i.InstallmentNumber == 3).DueDate.Should().Be(FirstDue.AddMonths(2));
    }

    [Fact]
    public void Create_ShouldRaisePlanCreatedEvent()
    {
        var plan = CreatePlan();

        plan.DomainEvents.Should().ContainSingle(e => e is InstallmentPlanCreatedEvent);
    }

    [Fact]
    public void Create_ShouldThrow_WhenInstallmentCountIsOne()
    {
        var act = () => InstallmentPlan.Create(TestCardId, TestAccountId, TestTxId,
            new Money(100m, Currency.TRY), 1, 0, FirstDue);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PayInstallment_ShouldMarkItemAsPaid()
    {
        var plan = CreatePlan(3);

        plan.PayInstallment(1, DateTime.UtcNow);

        plan.Items.First(i => i.InstallmentNumber == 1).Status.Should().Be(InstallmentItemStatus.Paid);
        plan.PaidCount.Should().Be(1);
    }

    [Fact]
    public void PayInstallment_ShouldCompleteplan_WhenAllPaid()
    {
        var plan = CreatePlan(2);

        plan.PayInstallment(1, DateTime.UtcNow);
        plan.PayInstallment(2, DateTime.UtcNow);

        plan.Status.Should().Be(InstallmentPlanStatus.Completed);
        plan.DomainEvents.Should().Contain(e => e is InstallmentPlanCompletedEvent);
    }

    [Fact]
    public void PayInstallment_ShouldThrow_WhenAlreadyPaid()
    {
        var plan = CreatePlan(3);
        plan.PayInstallment(1, DateTime.UtcNow);

        var act = () => plan.PayInstallment(1, DateTime.UtcNow);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled()
    {
        var plan = CreatePlan();

        plan.Cancel("Customer request");

        plan.Status.Should().Be(InstallmentPlanStatus.Cancelled);
        plan.DomainEvents.Should().Contain(e => e is InstallmentPlanCancelledEvent);
    }

    [Fact]
    public void Cancel_ShouldThrow_WhenAlreadyCancelled()
    {
        var plan = CreatePlan();
        plan.Cancel("First cancel");

        var act = () => plan.Cancel("Second cancel");

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
