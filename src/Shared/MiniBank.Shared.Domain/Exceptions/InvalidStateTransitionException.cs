namespace MiniBank.Shared.Domain.Exceptions;

public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entityType, string fromState, string toState)
        : base("INVALID_STATE_TRANSITION", $"{entityType}: transition from '{fromState}' to '{toState}' is not allowed.") { }
}
