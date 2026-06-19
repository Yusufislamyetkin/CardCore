namespace MiniBank.FraudService.Application;

public sealed record FraudScoringRequest(
    decimal Amount,
    string Currency,
    int TransactionHourUtc,
    int TransactionCountLastHour,
    int RecentDeclineCount,
    bool IsInternational);

public sealed record FraudRuleResult(
    string RuleName,
    string Description,
    decimal ScoreContribution);

public sealed record FraudScoringResult(
    decimal Score,
    IReadOnlyList<FraudRuleResult> TriggeredRules);

public sealed class FraudScoringEngine
{
    private const decimal HighAmountThresholdTry = 5000m;
    private const int NightHourStart = 2;
    private const int NightHourEnd = 5;
    private const int VelocityThreshold = 5;

    public FraudScoringResult Score(FraudScoringRequest request)
    {
        var rules = new List<FraudRuleResult>();
        decimal total = 0;

        // High single amount
        if (request.Amount >= HighAmountThresholdTry)
        {
            var contrib = Math.Min(30m, (request.Amount / HighAmountThresholdTry - 1m) * 10m + 20m);
            rules.Add(new FraudRuleResult("HIGH_AMOUNT",
                $"Amount {request.Amount} {request.Currency} exceeds threshold {HighAmountThresholdTry}", contrib));
            total += contrib;
        }

        // Night-time transaction
        if (request.TransactionHourUtc >= NightHourStart && request.TransactionHourUtc <= NightHourEnd)
        {
            rules.Add(new FraudRuleResult("UNUSUAL_TIME",
                $"Transaction at {request.TransactionHourUtc:D2}:xx UTC (night-time 02-05)", 15m));
            total += 15m;
        }

        // High velocity
        if (request.TransactionCountLastHour > VelocityThreshold)
        {
            var contrib = Math.Min(35m, (request.TransactionCountLastHour - VelocityThreshold) * 5m + 20m);
            rules.Add(new FraudRuleResult("HIGH_VELOCITY",
                $"{request.TransactionCountLastHour} transactions in last hour", contrib));
            total += contrib;
        }

        // Repeated declines before this transaction
        if (request.RecentDeclineCount >= 3)
        {
            rules.Add(new FraudRuleResult("REPEATED_DECLINES",
                $"{request.RecentDeclineCount} declined attempts recently", 20m));
            total += 20m;
        }

        // International transaction
        if (request.IsInternational)
        {
            rules.Add(new FraudRuleResult("INTERNATIONAL",
                "Cross-border transaction", 10m));
            total += 10m;
        }

        return new FraudScoringResult(Math.Min(100m, Math.Round(total, 2)), rules.AsReadOnly());
    }
}
