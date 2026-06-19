namespace MiniBank.Shared.Domain.Exceptions;

public sealed class CardNotActiveException : DomainException
{
    public CardNotActiveException(string cardId)
        : base("CARD_NOT_ACTIVE", $"Card '{cardId}' is not in Active status.") { }
}
