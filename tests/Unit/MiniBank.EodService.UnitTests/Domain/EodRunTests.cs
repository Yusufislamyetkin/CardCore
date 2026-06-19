using FluentAssertions;
using MiniBank.EodService.Domain.Entities;
using MiniBank.EodService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.EodService.UnitTests.Domain;

public sealed class EodRunTests
{
    private static readonly DateOnly TestDate = new(2026, 6, 19);

    [Fact]
    public void Schedule_ShouldCreateRunInScheduledStatus()
    {
        var run = EodRun.Schedule(TestDate);

        run.Status.Should().Be(EodRunStatus.Scheduled);
        run.ProcessingDate.Should().Be(TestDate);
        run.StartedAt.Should().BeNull();
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Schedule_ShouldRaiseEodRunScheduledEvent()
    {
        var run = EodRun.Schedule(TestDate);

        run.DomainEvents.Should().ContainSingle(e => e is EodRunScheduledEvent);
        var ev = (EodRunScheduledEvent)run.DomainEvents.First();
        ev.ProcessingDate.Should().Be(TestDate);
    }

    [Fact]
    public void Start_ShouldTransitionToRunning()
    {
        var run = EodRun.Schedule(TestDate);

        run.Start();

        run.Status.Should().Be(EodRunStatus.Running);
        run.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_ShouldRaiseEodRunStartedEvent()
    {
        var run = EodRun.Schedule(TestDate);

        run.Start();

        run.DomainEvents.Should().Contain(e => e is EodRunStartedEvent);
    }

    [Fact]
    public void Start_ShouldThrow_WhenNotScheduled()
    {
        var run = EodRun.Schedule(TestDate);
        run.Start();

        var act = () => run.Start();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Complete_ShouldTransitionToCompleted()
    {
        var run = EodRun.Schedule(TestDate);
        run.Start();

        run.Complete(150, 75000m);

        run.Status.Should().Be(EodRunStatus.Completed);
        run.ProcessedTransactionCount.Should().Be(150);
        run.TotalSettledAmount.Should().Be(75000m);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldRaiseEodRunCompletedEvent()
    {
        var run = EodRun.Schedule(TestDate);
        run.Start();

        run.Complete(10, 5000m);

        run.DomainEvents.Should().Contain(e => e is EodRunCompletedEvent);
        var ev = (EodRunCompletedEvent)run.DomainEvents.First(e => e is EodRunCompletedEvent);
        ev.ProcessedTransactionCount.Should().Be(10);
        ev.TotalSettledAmount.Should().Be(5000m);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenNotRunning()
    {
        var run = EodRun.Schedule(TestDate);

        var act = () => run.Complete(0, 0m);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Fail_ShouldTransitionToFailed()
    {
        var run = EodRun.Schedule(TestDate);
        run.Start();

        run.Fail("Database connection lost");

        run.Status.Should().Be(EodRunStatus.Failed);
        run.ErrorMessage.Should().Be("Database connection lost");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_ShouldThrow_WhenNotRunning()
    {
        var run = EodRun.Schedule(TestDate);

        var act = () => run.Fail("some error");

        act.Should().Throw<InvalidStateTransitionException>();
    }
}

public sealed class StatementTests
{
    private static readonly AccountId TestAccountId = AccountId.New();
    private static readonly EodRunId TestRunId = EodRunId.New();
    private static readonly Money ZeroTry = new(0m, Currency.TRY);
    private static readonly Money HundredTry = new(100m, Currency.TRY);

    [Fact]
    public void Generate_ShouldCreateStatementWithCorrectValues()
    {
        var stmt = Statement.Generate(
            TestAccountId, TestRunId, 2026, 6,
            openingBalance: HundredTry,
            closingBalance: new Money(200m, Currency.TRY),
            totalDebits: ZeroTry,
            totalCredits: HundredTry,
            transactionCount: 5);

        stmt.AccountId.Should().Be(TestAccountId);
        stmt.Year.Should().Be(2026);
        stmt.Month.Should().Be(6);
        stmt.OpeningBalance.Amount.Should().Be(100m);
        stmt.ClosingBalance.Amount.Should().Be(200m);
        stmt.TotalCredits.Amount.Should().Be(100m);
        stmt.TransactionCount.Should().Be(5);
    }

    [Fact]
    public void Generate_ShouldRaiseStatementGeneratedEvent()
    {
        var stmt = Statement.Generate(
            TestAccountId, TestRunId, 2026, 6,
            ZeroTry, ZeroTry, ZeroTry, ZeroTry, 0);

        stmt.DomainEvents.Should().ContainSingle(e => e is StatementGeneratedEvent);
        var ev = (StatementGeneratedEvent)stmt.DomainEvents.First();
        ev.Year.Should().Be(2026);
        ev.Month.Should().Be(6);
    }

    [Fact]
    public void Generate_ShouldThrow_WhenYearOutOfRange()
    {
        var act = () => Statement.Generate(
            TestAccountId, TestRunId, 1999, 1,
            ZeroTry, ZeroTry, ZeroTry, ZeroTry, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_ShouldThrow_WhenMonthOutOfRange()
    {
        var act = () => Statement.Generate(
            TestAccountId, TestRunId, 2026, 13,
            ZeroTry, ZeroTry, ZeroTry, ZeroTry, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
