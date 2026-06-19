using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.CardService.Domain.Entities;

public sealed class CardLimit
{
    public Guid Id { get; private set; }
    public CardId CardId { get; private set; } = default!;
    public LimitType LimitType { get; private set; }
    public Money Amount { get; private set; } = default!;
    public DateTime UpdatedAt { get; private set; }

    private CardLimit() { }

    public static CardLimit Create(CardId cardId, LimitType limitType, Money amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            LimitType = limitType,
            Amount = amount,
            UpdatedAt = DateTime.UtcNow
        };

    public void Update(Money newAmount)
    {
        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;
    }
}
