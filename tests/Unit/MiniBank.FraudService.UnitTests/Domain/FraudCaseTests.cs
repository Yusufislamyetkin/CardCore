using FluentAssertions;
using MiniBank.FraudService.Application;
using MiniBank.FraudService.Domain.Entities;
using MiniBank.FraudService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using Xunit;

namespace MiniBank.FraudService.UnitTests.Domain;

public sealed class FraudCaseTests
{
    private static readonly CardId TestCardId = CardId.New();
    private static readonly AccountId TestAccountId = AccountId.New();
    private static readonly TransactionId TestTxId = TransactionId.New();
    private static readonly Money TestAmount = new(1000m, Currency.TRY);

    private static FraudCase CreateCase(decimal riskScore = 25m,
        IEnumerable<(string, string, decimal)>? rules = null) =>
        FraudCase.Create(TestTxId, TestCardId, TestAccountId, TestAmount,
            riskScore, rules ?? Array.Empty<(string, string, decimal)>());

    [Fact]
    public void Create_ShouldSetStatusOpen()
    {
        var fraudCase = CreateCase();

        fraudCase.Status.Should().Be(FraudCaseStatus.Open);
    }

    [Fact]
    public void Create_ShouldRaiseFraudCaseCreatedEvent()
    {
        var fraudCase = CreateCase(40m);

        fraudCase.DomainEvents.Should().ContainSingle(e => e is FraudCaseCreatedEvent);
        var ev = (FraudCaseCreatedEvent)fraudCase.DomainEvents.First();
        ev.RiskScore.Should().Be(40m);
    }

    [Fact]
    public void Create_ShouldPopulateFlags_WhenRulesProvided()
    {
        var rules = new[]
        {
            ("HIGH_AMOUNT", "Amount exceeds threshold", 20m),
            ("UNUSUAL_TIME", "Night-time transaction", 15m)
        };

        var fraudCase = FraudCase.Create(TestTxId, TestCardId, TestAccountId,
            TestAmount, 35m, rules);

        fraudCase.Flags.Should().HaveCount(2);
        fraudCase.Flags.Should().Contain(f => f.RuleName == "HIGH_AMOUNT");
        fraudCase.Flags.Should().Contain(f => f.RuleName == "UNUSUAL_TIME");
    }

    [Fact]
    public void Create_ShouldThrow_WhenRiskScoreOutOfRange()
    {
        var act = () => CreateCase(riskScore: 101m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenRiskScoreNegative()
    {
        var act = () => CreateCase(riskScore: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StartInvestigation_ShouldChangeStatusToInvestigating()
    {
        var fraudCase = CreateCase();

        fraudCase.StartInvestigation();

        fraudCase.Status.Should().Be(FraudCaseStatus.Investigating);
        fraudCase.DomainEvents.Should().Contain(e => e is FraudCaseInvestigatingEvent);
    }

    [Fact]
    public void StartInvestigation_ShouldThrow_WhenNotOpen()
    {
        var fraudCase = CreateCase();
        fraudCase.StartInvestigation();

        var act = () => fraudCase.StartInvestigation();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Resolve_ShouldSetResolutionAndStatus()
    {
        var fraudCase = CreateCase();
        fraudCase.StartInvestigation();

        fraudCase.Resolve(FraudResolution.Confirmed, "agent@bank.com", "PAN confirmed stolen");

        fraudCase.Status.Should().Be(FraudCaseStatus.Resolved);
        fraudCase.Resolution.Should().Be(FraudResolution.Confirmed);
        fraudCase.ResolvedBy.Should().Be("agent@bank.com");
        fraudCase.ResolutionNote.Should().Be("PAN confirmed stolen");
        fraudCase.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_CanResolveFromOpen_WhenNoInvestigationStarted()
    {
        var fraudCase = CreateCase();

        fraudCase.Resolve(FraudResolution.Cleared, "system", null);

        fraudCase.Status.Should().Be(FraudCaseStatus.Resolved);
        fraudCase.Resolution.Should().Be(FraudResolution.Cleared);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenAlreadyResolved()
    {
        var fraudCase = CreateCase();
        fraudCase.Resolve(FraudResolution.Cleared, "agent", null);

        var act = () => fraudCase.Resolve(FraudResolution.Confirmed, "agent2", null);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Resolve_ShouldRaiseFraudCaseResolvedEvent()
    {
        var fraudCase = CreateCase();

        fraudCase.Resolve(FraudResolution.Cleared, "agent@bank.com", null);

        fraudCase.DomainEvents.Should().Contain(e => e is FraudCaseResolvedEvent);
        var ev = (FraudCaseResolvedEvent)fraudCase.DomainEvents
            .First(e => e is FraudCaseResolvedEvent);
        ev.ResolvedBy.Should().Be("agent@bank.com");
        ev.Resolution.Should().Be("Cleared");
    }
}

public sealed class FraudScoringEngineTests
{
    private readonly FraudScoringEngine _engine = new();

    [Fact]
    public void Score_ShouldReturnZero_WhenNoRulesTriggered()
    {
        var request = new FraudScoringRequest(100m, "TRY", 12, 1, 0, false);

        var result = _engine.Score(request);

        result.Score.Should().Be(0m);
        result.TriggeredRules.Should().BeEmpty();
    }

    [Fact]
    public void Score_ShouldTriggerHighAmountRule()
    {
        var request = new FraudScoringRequest(6000m, "TRY", 12, 1, 0, false);

        var result = _engine.Score(request);

        result.TriggeredRules.Should().Contain(r => r.RuleName == "HIGH_AMOUNT");
        result.Score.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Score_ShouldTriggerNightTimeRule()
    {
        var request = new FraudScoringRequest(100m, "TRY", 3, 1, 0, false);

        var result = _engine.Score(request);

        result.TriggeredRules.Should().Contain(r => r.RuleName == "UNUSUAL_TIME");
    }

    [Fact]
    public void Score_ShouldTriggerVelocityRule()
    {
        var request = new FraudScoringRequest(100m, "TRY", 12, 10, 0, false);

        var result = _engine.Score(request);

        result.TriggeredRules.Should().Contain(r => r.RuleName == "HIGH_VELOCITY");
    }

    [Fact]
    public void Score_ShouldCapAt100()
    {
        var request = new FraudScoringRequest(50000m, "TRY", 3, 20, 5, true);

        var result = _engine.Score(request);

        result.Score.Should().Be(100m);
    }

    [Fact]
    public void Score_ShouldTriggerInternationalRule()
    {
        var request = new FraudScoringRequest(100m, "TRY", 12, 1, 0, IsInternational: true);

        var result = _engine.Score(request);

        result.TriggeredRules.Should().Contain(r => r.RuleName == "INTERNATIONAL");
        result.Score.Should().Be(10m);
    }
}
