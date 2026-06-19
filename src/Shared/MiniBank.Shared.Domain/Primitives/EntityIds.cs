namespace MiniBank.Shared.Domain.Primitives;

public sealed record AccountId(Guid Value) : StronglyTypedId(Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId From(Guid id) => new(id);
}

public sealed record CardId(Guid Value) : StronglyTypedId(Value)
{
    public static CardId New() => new(Guid.NewGuid());
    public static CardId From(Guid id) => new(id);
}

public sealed record CustomerId(Guid Value) : StronglyTypedId(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(Guid id) => new(id);
}

public sealed record TransactionId(Guid Value) : StronglyTypedId(Value)
{
    public static TransactionId New() => new(Guid.NewGuid());
    public static TransactionId From(Guid id) => new(id);
}

public sealed record AuthorizationId(Guid Value) : StronglyTypedId(Value)
{
    public static AuthorizationId New() => new(Guid.NewGuid());
    public static AuthorizationId From(Guid id) => new(id);
}

public sealed record InstallmentPlanId(Guid Value) : StronglyTypedId(Value)
{
    public static InstallmentPlanId New() => new(Guid.NewGuid());
    public static InstallmentPlanId From(Guid id) => new(id);
}

public sealed record UserId(Guid Value) : StronglyTypedId(Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid id) => new(id);
}

public sealed record LedgerEntryId(Guid Value) : StronglyTypedId(Value)
{
    public static LedgerEntryId New() => new(Guid.NewGuid());
    public static LedgerEntryId From(Guid id) => new(id);
}

public sealed record FraudCaseId(Guid Value) : StronglyTypedId(Value)
{
    public static FraudCaseId New() => new(Guid.NewGuid());
    public static FraudCaseId From(Guid id) => new(id);
}

public sealed record FraudFlagId(Guid Value) : StronglyTypedId(Value)
{
    public static FraudFlagId New() => new(Guid.NewGuid());
    public static FraudFlagId From(Guid id) => new(id);
}

public sealed record EodRunId(Guid Value) : StronglyTypedId(Value)
{
    public static EodRunId New() => new(Guid.NewGuid());
    public static EodRunId From(Guid id) => new(id);
}

public sealed record StatementId(Guid Value) : StronglyTypedId(Value)
{
    public static StatementId New() => new(Guid.NewGuid());
    public static StatementId From(Guid id) => new(id);
}

public sealed record ReconciliationBatchId(Guid Value) : StronglyTypedId(Value)
{
    public static ReconciliationBatchId New() => new(Guid.NewGuid());
    public static ReconciliationBatchId From(Guid id) => new(id);
}

public sealed record ReconciliationItemId(Guid Value) : StronglyTypedId(Value)
{
    public static ReconciliationItemId New() => new(Guid.NewGuid());
    public static ReconciliationItemId From(Guid id) => new(id);
}

public sealed record NotificationId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
    public static NotificationId From(Guid id) => new(id);
}
