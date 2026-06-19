namespace MiniBank.Shared.Infrastructure.Persistence;

public interface IAggregateWithEvents
{
    IReadOnlyCollection<object> GetDomainEvents();
    void ClearEvents();
}
