using MiniBank.CardService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.CardService.Domain.Entities;

public sealed class Card : AggregateRoot<CardId>
{
    private readonly List<CardLimit> _limits = [];

    public AccountId AccountId { get; private set; } = default!;
    public CustomerId CustomerId { get; private set; } = default!;

    // PCI-DSS: never store raw PAN; store only hash + masked
    public string PanHash { get; private set; } = string.Empty;
    public string PanMasked { get; private set; } = string.Empty;
    public string Last4 { get; private set; } = string.Empty;
    public string Bin { get; private set; } = string.Empty;

    public CardExpiry Expiry { get; private set; } = default!;
    public CardType CardType { get; private set; }
    public CardStatus Status { get; private set; }
    public string? BlockReason { get; private set; }

    public IReadOnlyCollection<CardLimit> Limits => _limits.AsReadOnly();

    private Card() { }

    public static Card Issue(AccountId accountId, CustomerId customerId, Pan pan, CardExpiry expiry, CardType cardType)
    {
        var card = new Card
        {
            Id = CardId.New(),
            AccountId = accountId,
            CustomerId = customerId,
            PanHash = pan.Hashed,
            PanMasked = pan.Masked,
            Last4 = pan.Last4,
            Bin = pan.Bin,
            Expiry = expiry,
            CardType = cardType,
            Status = CardStatus.Created
        };

        card.RaiseDomainEvent(new CardIssuedEvent(
            card.Id.Value,
            accountId.Value,
            customerId.Value,
            pan.Masked,
            cardType));

        return card;
    }

    public void Activate(DateTime utcNow)
    {
        if (Status != CardStatus.Created)
            throw new InvalidStateTransitionException("Card", Status.ToString(), "Active");
        if (Expiry.IsExpired(utcNow))
            throw new InvalidStateTransitionException("Card", "Activate", "card is expired");

        Status = CardStatus.Active;
        RaiseDomainEvent(new CardActivatedEvent(Id.Value));
        MarkModified();
    }

    public void Block(string reason)
    {
        if (Status is CardStatus.Closed or CardStatus.StolenLost)
            throw new InvalidStateTransitionException("Card", Status.ToString(), "Blocked");

        BlockReason = reason;
        Status = CardStatus.Blocked;
        RaiseDomainEvent(new CardBlockedEvent(Id.Value, reason));
        MarkModified();
    }

    public void Unblock()
    {
        if (Status != CardStatus.Blocked)
            throw new InvalidStateTransitionException("Card", Status.ToString(), "Active");

        BlockReason = null;
        Status = CardStatus.Active;
        RaiseDomainEvent(new CardUnblockedEvent(Id.Value));
        MarkModified();
    }

    public void ReportStolenOrLost()
    {
        if (Status is CardStatus.Closed or CardStatus.StolenLost)
            throw new InvalidStateTransitionException("Card", Status.ToString(), "StolenLost");

        Status = CardStatus.StolenLost;
        RaiseDomainEvent(new CardStolenLostEvent(Id.Value));
        MarkModified();
    }

    public void Close()
    {
        if (Status == CardStatus.Closed)
            throw new InvalidStateTransitionException("Card", "Closed", "Closed");

        Status = CardStatus.Closed;
        RaiseDomainEvent(new CardClosedEvent(Id.Value));
        MarkModified();
    }

    public void SetLimit(LimitType limitType, Money amount)
    {
        var existing = _limits.FirstOrDefault(l => l.LimitType == limitType);
        if (existing is not null)
            existing.Update(amount);
        else
            _limits.Add(CardLimit.Create(Id, limitType, amount));

        RaiseDomainEvent(new CardLimitUpdatedEvent(Id.Value, limitType, amount.Amount, amount.Currency));
        MarkModified();
    }

    public bool IsUsable(DateTime utcNow) =>
        Status == CardStatus.Active && !Expiry.IsExpired(utcNow);
}

