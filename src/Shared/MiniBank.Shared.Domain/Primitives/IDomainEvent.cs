namespace MiniBank.Shared.Domain.Primitives;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
