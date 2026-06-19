using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;

namespace MiniBank.InstallmentService.Domain.Entities;

public enum InstallmentItemStatus { Pending = 1, Paid = 2, Overdue = 3 }

public sealed class InstallmentItem
{
    public Guid Id { get; private set; }
    public InstallmentPlanId PlanId { get; private set; } = default!;
    public int InstallmentNumber { get; private set; }
    public Money Amount { get; private set; } = default!;
    public DateTime DueDate { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public InstallmentItemStatus Status { get; private set; }

    private InstallmentItem() { }

    public static InstallmentItem Create(InstallmentPlanId planId, int number, Money amount, DateTime dueDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            InstallmentNumber = number,
            Amount = amount,
            DueDate = dueDate,
            Status = InstallmentItemStatus.Pending
        };

    public void MarkPaid(DateTime paidAt)
    {
        Status = InstallmentItemStatus.Paid;
        PaidAt = paidAt;
    }

    public void MarkOverdue()
    {
        if (Status == InstallmentItemStatus.Pending)
            Status = InstallmentItemStatus.Overdue;
    }
}
