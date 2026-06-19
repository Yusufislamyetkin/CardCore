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
