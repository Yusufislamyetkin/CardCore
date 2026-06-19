namespace MiniBank.Shared.Domain.Exceptions;

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string entityType, string entityId)
        : base("CONCURRENCY_CONFLICT", $"{entityType} '{entityId}' was modified by another process. Please retry.") { }
}
