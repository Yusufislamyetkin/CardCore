namespace MiniBank.Shared.Domain.Primitives;

public abstract record StronglyTypedId(Guid Value)
{
    public sealed override string ToString() => Value.ToString();
}
