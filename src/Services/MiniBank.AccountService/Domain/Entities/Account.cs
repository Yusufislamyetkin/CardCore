using MiniBank.AccountService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.AccountService.Domain.Entities;

public sealed class Account : AggregateRoot<AccountId>
{
    private readonly List<LedgerEntry> _ledgerEntries = [];

    public CustomerId CustomerId { get; private set; } = default!;
    public Iban Iban { get; private set; } = default!;
    public Money Balance { get; private set; } = default!;
    public AccountType AccountType { get; private set; }
    public AccountStatus Status { get; private set; }

    public IReadOnlyCollection<LedgerEntry> LedgerEntries => _ledgerEntries.AsReadOnly();

    private Account() { }

    public static Account Create(CustomerId customerId, Iban iban, Currency currency, AccountType accountType)
    {
        var account = new Account
        {
            Id = AccountId.New(),
            CustomerId = customerId,
            Iban = iban,
            Balance = Money.Zero(currency),
            AccountType = accountType,
            Status = AccountStatus.Active
        };

        account.RaiseDomainEvent(new AccountCreatedEvent(
            account.Id.Value,
            customerId.Value,
            iban.Value,
            currency,
            accountType));

        return account;
    }

    public void Deposit(Money amount, string description, string? transactionRef = null)
    {
        EnsureActive();

        Balance += amount;
        var entry = LedgerEntry.Create(Id, EntryType.Credit, amount, Balance, description, transactionRef);
        _ledgerEntries.Add(entry);

        RaiseDomainEvent(new MoneyDepositedEvent(Id.Value, amount.Amount, amount.Currency, Balance.Amount));
        MarkModified();
    }

    public void Withdraw(Money amount, string description, string? transactionRef = null)
    {
        EnsureActive();

        if (Balance < amount)
            throw new InsufficientFundsException(Balance, amount);

        Balance -= amount;
        var entry = LedgerEntry.Create(Id, EntryType.Debit, amount, Balance, description, transactionRef);
        _ledgerEntries.Add(entry);

        RaiseDomainEvent(new MoneyWithdrawnEvent(Id.Value, amount.Amount, amount.Currency, Balance.Amount));
        MarkModified();
    }

    public void Freeze()
    {
        if (Status == AccountStatus.Frozen)
            throw new InvalidStateTransitionException("Account", "Freeze", "already frozen");
        if (Status == AccountStatus.Closed)
            throw new InvalidStateTransitionException("Account", "Freeze", "account is closed");

        Status = AccountStatus.Frozen;
        RaiseDomainEvent(new AccountFrozenEvent(Id.Value));
        MarkModified();
    }

    public void Unfreeze()
    {
        if (Status != AccountStatus.Frozen)
            throw new InvalidStateTransitionException("Account", "Unfreeze", "account is not frozen");

        Status = AccountStatus.Active;
        RaiseDomainEvent(new AccountUnfrozenEvent(Id.Value));
        MarkModified();
    }

    public void Close()
    {
        if (Status == AccountStatus.Closed)
            throw new InvalidStateTransitionException("Account", "Close", "already closed");
        if (Balance.Amount > 0)
            throw new InvalidStateTransitionException("Account", "Close", "balance must be zero before closing");

        Status = AccountStatus.Closed;
        RaiseDomainEvent(new AccountClosedEvent(Id.Value));
        MarkModified();
    }

    private void EnsureActive()
    {
        if (Status == AccountStatus.Frozen)
            throw new AccountFrozenException(Id.Value.ToString());
        if (Status == AccountStatus.Closed)
            throw new InvalidStateTransitionException("Account", "operation", "account is closed");
    }
}
