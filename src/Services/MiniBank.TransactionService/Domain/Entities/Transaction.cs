using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.TransactionService.Domain.Events;

namespace MiniBank.TransactionService.Domain.Entities;

public sealed class Transaction : AggregateRoot<TransactionId>
{
    public CardId CardId { get; private set; } = default!;
    public AccountId AccountId { get; private set; } = default!;
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public Money Amount { get; private set; } = default!;
    public TransactionType TransactionType { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string MerchantName { get; private set; } = string.Empty;
    public string? ReferenceId { get; private set; }
    public string? ReverseReason { get; private set; }
    public DateTime? SettledAt { get; private set; }

    private Transaction() { }

    private static Transaction Build(
        CardId cardId, AccountId accountId, Money amount, TransactionType type,
        string merchantId, string merchantName,
        string? authorizationId, string? authorizationCode, string? referenceId)
    {
        var tx = new Transaction
        {
            Id = TransactionId.New(),
            CardId = cardId,
            AccountId = accountId,
            Amount = amount,
            TransactionType = type,
            Status = TransactionStatus.Approved,
            MerchantId = merchantId,
            MerchantName = merchantName,
            AuthorizationId = authorizationId,
            AuthorizationCode = authorizationCode,
            ReferenceId = referenceId
        };

        tx.RaiseDomainEvent(new TransactionCreatedEvent(
            tx.Id.Value, cardId.Value, accountId.Value,
            amount.Amount, amount.Currency, type, authorizationCode));

        return tx;
    }

    public static Transaction Create(
        CardId cardId, AccountId accountId, Money amount, TransactionType type,
        string merchantId, string merchantName,
        string? authorizationId = null, string? authorizationCode = null) =>
        Build(cardId, accountId, amount, type, merchantId, merchantName,
            authorizationId, authorizationCode, null);

    public void Settle()
    {
        if (Status != TransactionStatus.Approved)
            throw new InvalidStateTransitionException("Transaction", Status.ToString(), "Settled");

        Status = TransactionStatus.Settled;
        SettledAt = DateTime.UtcNow;
        RaiseDomainEvent(new TransactionSettledEvent(
            Id.Value, AccountId.Value, Amount.Amount, Amount.Currency));
        MarkModified();
    }

    public void Reverse(string reason)
    {
        if (Status is not (TransactionStatus.Approved or TransactionStatus.Settled))
            throw new InvalidStateTransitionException("Transaction", Status.ToString(), "Reversed");

        ReverseReason = reason;
        Status = TransactionStatus.Reversed;
        RaiseDomainEvent(new TransactionReversedEvent(
            Id.Value, AccountId.Value, Amount.Amount, Amount.Currency, reason));
        MarkModified();
    }

    public Transaction Refund(Money refundAmount)
    {
        if (Status != TransactionStatus.Settled)
            throw new InvalidStateTransitionException("Transaction", Status.ToString(), "Refunded");

        if (refundAmount > Amount)
            throw new InvalidStateTransitionException("Transaction", "Refund", "refund exceeds original amount");

        Status = TransactionStatus.Refunded;
        MarkModified();

        var refundTx = Build(CardId, AccountId, refundAmount, TransactionType.Refund,
            MerchantId, MerchantName, null, null, Id.Value.ToString());

        RaiseDomainEvent(new TransactionRefundedEvent(
            Id.Value, refundTx.Id.Value, refundAmount.Amount, refundAmount.Currency));

        return refundTx;
    }
}
