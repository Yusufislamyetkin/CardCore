using MiniBank.AuthorizationService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AuthorizationService.Domain.Entities;

public enum AuthorizationStatus { Approved = 1, Declined = 2, Reversed = 3 }

public sealed class Authorization : AggregateRoot<AuthorizationId>
{
    public CardId CardId { get; private set; } = default!;
    public AccountId AccountId { get; private set; } = default!;
    public string PanHash { get; private set; } = string.Empty;
    public string MerchantId { get; private set; } = string.Empty;
    public string MerchantName { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = default!;
    public AuthorizationStatus Status { get; private set; }
    public DeclineReason DeclineReason { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public string? ReverseReason { get; private set; }

    private Authorization() { }

    public static Authorization Approve(
        CardId cardId,
        AccountId accountId,
        string panHash,
        Money amount,
        string merchantId,
        string merchantName)
    {
        var auth = new Authorization
        {
            Id = AuthorizationId.New(),
            CardId = cardId,
            AccountId = accountId,
            PanHash = panHash,
            Amount = amount,
            MerchantId = merchantId,
            MerchantName = merchantName,
            Status = AuthorizationStatus.Approved,
            DeclineReason = DeclineReason.None,
            AuthorizationCode = GenerateAuthCode()
        };

        auth.RaiseDomainEvent(new AuthorizationApprovedEvent(
            auth.Id.Value,
            cardId.Value,
            accountId.Value,
            amount.Amount,
            amount.Currency,
            auth.AuthorizationCode!));

        return auth;
    }

    public static Authorization Decline(
        CardId cardId,
        AccountId accountId,
        string panHash,
        Money amount,
        string merchantId,
        string merchantName,
        DeclineReason reason)
    {
        var auth = new Authorization
        {
            Id = AuthorizationId.New(),
            CardId = cardId,
            AccountId = accountId,
            PanHash = panHash,
            Amount = amount,
            MerchantId = merchantId,
            MerchantName = merchantName,
            Status = AuthorizationStatus.Declined,
            DeclineReason = reason,
            AuthorizationCode = null
        };

        auth.RaiseDomainEvent(new AuthorizationDeclinedEvent(
            auth.Id.Value,
            cardId.Value,
            amount.Amount,
            amount.Currency,
            reason));

        return auth;
    }

    public void Reverse(string reason)
    {
        if (Status != AuthorizationStatus.Approved)
            throw new MiniBank.Shared.Domain.Exceptions.InvalidStateTransitionException(
                "Authorization", Status.ToString(), "Reversed");

        ReverseReason = reason;
        Status = AuthorizationStatus.Reversed;
        RaiseDomainEvent(new AuthorizationReversedEvent(Id.Value, CardId.Value, Amount.Amount, Amount.Currency, reason));
        MarkModified();
    }

    private static string GenerateAuthCode() =>
        Random.Shared.Next(100000, 999999).ToString();
}
