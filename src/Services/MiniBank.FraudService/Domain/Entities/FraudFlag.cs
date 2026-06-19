using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.FraudService.Domain.Entities;

public sealed class FraudFlag : Entity<FraudFlagId>
{
    public FraudCaseId CaseId { get; private set; } = null!;
    public string RuleName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal ScoreContribution { get; private set; }

    private FraudFlag() : base(FraudFlagId.New()) { }

    internal static FraudFlag Create(FraudCaseId caseId, string ruleName, string description, decimal scoreContribution)
        => new(FraudFlagId.New())
        {
            CaseId = caseId,
            RuleName = ruleName,
            Description = description,
            ScoreContribution = scoreContribution
        };

    private FraudFlag(FraudFlagId id) : base(id) { }
}
