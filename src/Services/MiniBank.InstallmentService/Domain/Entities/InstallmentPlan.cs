using MiniBank.InstallmentService.Domain.Events;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.InstallmentService.Domain.Entities;

public enum InstallmentPlanStatus { Active = 1, Completed = 2, Cancelled = 3 }

public sealed class InstallmentPlan : AggregateRoot<InstallmentPlanId>
{
    private readonly List<InstallmentItem> _items = [];

    public CardId CardId { get; private set; } = default!;
    public AccountId AccountId { get; private set; } = default!;
    public TransactionId TransactionId { get; private set; } = default!;
    public Money TotalAmount { get; private set; } = default!;
    public Money InstallmentAmount { get; private set; } = default!;
    public int InstallmentCount { get; private set; }
    public int PaidCount { get; private set; }
    public decimal InterestRate { get; private set; }
    public InstallmentPlanStatus Status { get; private set; }

    public IReadOnlyCollection<InstallmentItem> Items => _items.AsReadOnly();

    private InstallmentPlan() { }

    public static InstallmentPlan Create(
        CardId cardId,
        AccountId accountId,
        TransactionId transactionId,
        Money principalAmount,
        int installmentCount,
        decimal annualInterestRate,
        DateTime firstDueDate)
    {
        if (installmentCount is < 2 or > 60)
            throw new ArgumentException("Installment count must be between 2 and 60.", nameof(installmentCount));

        var monthlyRate = annualInterestRate / 12 / 100;
        var monthlyPayment = monthlyRate > 0
            ? principalAmount.Amount * monthlyRate / (1 - (decimal)Math.Pow(1 + (double)monthlyRate, -installmentCount))
            : principalAmount.Amount / installmentCount;

        monthlyPayment = Math.Round(monthlyPayment, 2);
        var totalAmount = new Money(monthlyPayment * installmentCount, principalAmount.Currency);
        var installmentAmount = new Money(monthlyPayment, principalAmount.Currency);

        var plan = new InstallmentPlan
        {
            Id = InstallmentPlanId.New(),
            CardId = cardId,
            AccountId = accountId,
            TransactionId = transactionId,
            TotalAmount = totalAmount,
            InstallmentAmount = installmentAmount,
            InstallmentCount = installmentCount,
            PaidCount = 0,
            InterestRate = annualInterestRate,
            Status = InstallmentPlanStatus.Active
        };

        for (var i = 1; i <= installmentCount; i++)
        {
            var dueDate = firstDueDate.AddMonths(i - 1);
            plan._items.Add(InstallmentItem.Create(plan.Id, i, installmentAmount, dueDate));
        }

        plan.RaiseDomainEvent(new InstallmentPlanCreatedEvent(
            plan.Id.Value, cardId.Value, accountId.Value, transactionId.Value,
            totalAmount.Amount, principalAmount.Currency, installmentCount));

        return plan;
    }

    public void PayInstallment(int installmentNumber, DateTime paidAt)
    {
        if (Status != InstallmentPlanStatus.Active)
            throw new InvalidStateTransitionException("InstallmentPlan", Status.ToString(), "PayInstallment");

        var item = _items.FirstOrDefault(i => i.InstallmentNumber == installmentNumber)
            ?? throw new ArgumentException($"Installment {installmentNumber} not found.");

        if (item.Status == InstallmentItemStatus.Paid)
            throw new InvalidStateTransitionException("Installment", "Paid", "Paid");

        item.MarkPaid(paidAt);
        PaidCount++;

        RaiseDomainEvent(new InstallmentPaidEvent(
            Id.Value, installmentNumber, item.Amount.Amount, item.Amount.Currency));

        if (PaidCount == InstallmentCount)
        {
            Status = InstallmentPlanStatus.Completed;
            RaiseDomainEvent(new InstallmentPlanCompletedEvent(Id.Value));
        }

        MarkModified();
    }

    public void Cancel(string reason)
    {
        if (Status != InstallmentPlanStatus.Active)
            throw new InvalidStateTransitionException("InstallmentPlan", Status.ToString(), "Cancelled");

        Status = InstallmentPlanStatus.Cancelled;
        RaiseDomainEvent(new InstallmentPlanCancelledEvent(Id.Value, reason));
        MarkModified();
    }
}
