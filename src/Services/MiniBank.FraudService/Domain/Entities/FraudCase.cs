using MiniBank.FraudService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.FraudService.Domain.Entities;

public enum FraudCaseStatus { Open = 1, Investigating = 2, Resolved = 3 }
public enum FraudResolution { Cleared = 1, Confirmed = 2 }

public sealed class FraudCase : AggregateRoot<FraudCaseId>
{
    private readonly List<FraudFlag> _flags = new();

    public TransactionId TransactionId { get; private set; } = null!;
    public CardId CardId { get; private set; } = null!;
    public AccountId AccountId { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public decimal RiskScore { get; private set; }
    public FraudCaseStatus Status { get; private set; }
    public FraudResolution? Resolution { get; private set; }
    public string? ResolutionNote { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public IReadOnlyList<FraudFlag> Flags => _flags.AsReadOnly();

    private FraudCase() { }

    public static FraudCase Create(
        TransactionId transactionId,
        CardId cardId,
        AccountId accountId,
        Money amount,
        decimal riskScore,
        IEnumerable<(string ruleName, string description, decimal contribution)> triggeredRules)
    {
        if (riskScore < 0 || riskScore > 100)
            throw new ArgumentOutOfRangeException(nameof(riskScore), "Risk score must be between 0 and 100");

        var caseId = FraudCaseId.New();
        var fraudCase = new FraudCase
        {
            Id = caseId,
            TransactionId = transactionId,
            CardId = cardId,
            AccountId = accountId,
            Amount = amount,
            RiskScore = riskScore,
            Status = FraudCaseStatus.Open
        };

        foreach (var (ruleName, description, contribution) in triggeredRules)
            fraudCase._flags.Add(FraudFlag.Create(caseId, ruleName, description, contribution));

        fraudCase.RaiseDomainEvent(new FraudCaseCreatedEvent(caseId, transactionId, cardId, riskScore));
        return fraudCase;
    }

    public void StartInvestigation()
    {
        if (Status != FraudCaseStatus.Open)
            throw new InvalidStateTransitionException("FraudCase", Status.ToString(), FraudCaseStatus.Investigating.ToString());

        Status = FraudCaseStatus.Investigating;
        MarkModified();
        RaiseDomainEvent(new FraudCaseInvestigatingEvent(Id));
    }

    public void Resolve(FraudResolution resolution, string resolvedBy, string? note = null)
    {
        if (Status == FraudCaseStatus.Resolved)
            throw new InvalidStateTransitionException("FraudCase", Status.ToString(), FraudCaseStatus.Resolved.ToString());

        Status = FraudCaseStatus.Resolved;
        Resolution = resolution;
        ResolvedBy = resolvedBy;
        ResolutionNote = note;
        ResolvedAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new FraudCaseResolvedEvent(Id, resolution.ToString(), resolvedBy));
    }
}
