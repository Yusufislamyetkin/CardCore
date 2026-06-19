using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.ReconciliationService.Domain.Entities;

public enum ReconciliationItemStatus { Pending = 1, Matched = 2, Unmatched = 3, Disputed = 4, ManuallyResolved = 5 }

public sealed class ReconciliationItem : Entity<ReconciliationItemId>
{
    public ReconciliationBatchId BatchId { get; private set; } = null!;
    public string ExternalTransactionId { get; private set; } = string.Empty;
    public Guid? InternalTransactionId { get; private set; }
    public decimal ExternalAmount { get; private set; }
    public decimal InternalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal DiscrepancyAmount { get; private set; }
    public ReconciliationItemStatus Status { get; private set; }
    public string? ResolutionNote { get; private set; }

    private ReconciliationItem() : base(ReconciliationItemId.New()) { }

    internal static ReconciliationItem Create(
        ReconciliationBatchId batchId,
        string externalTransactionId,
        decimal externalAmount,
        string currency)
        => new(ReconciliationItemId.New())
        {
            BatchId = batchId,
            ExternalTransactionId = externalTransactionId,
            ExternalAmount = externalAmount,
            InternalAmount = 0m,
            Currency = currency,
            Status = ReconciliationItemStatus.Pending
        };

    internal void Match(Guid internalTransactionId, decimal internalAmount)
    {
        InternalTransactionId = internalTransactionId;
        InternalAmount = internalAmount;
        DiscrepancyAmount = Math.Abs(ExternalAmount - internalAmount);
        Status = DiscrepancyAmount == 0m
            ? ReconciliationItemStatus.Matched
            : ReconciliationItemStatus.Disputed;
        MarkModified();
    }

    internal void MarkUnmatched()
    {
        Status = ReconciliationItemStatus.Unmatched;
        DiscrepancyAmount = ExternalAmount;
        MarkModified();
    }

    internal void Resolve(string note)
    {
        Status = ReconciliationItemStatus.ManuallyResolved;
        ResolutionNote = note;
        MarkModified();
    }

    private ReconciliationItem(ReconciliationItemId id) : base(id) { }
}
