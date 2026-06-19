using MiniBank.ReconciliationService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.ReconciliationService.Domain.Entities;

public enum ReconciliationBatchStatus { Open = 1, Processing = 2, Completed = 3, Failed = 4 }

public sealed class ReconciliationBatch : AggregateRoot<ReconciliationBatchId>
{
    private readonly List<ReconciliationItem> _items = new();

    public DateOnly ReconciliationDate { get; private set; }
    public string NetworkSource { get; private set; } = string.Empty;
    public ReconciliationBatchStatus Status { get; private set; }
    public int MatchedCount { get; private set; }
    public int UnmatchedCount { get; private set; }
    public int DisputedCount { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyList<ReconciliationItem> Items => _items.AsReadOnly();

    private ReconciliationBatch() { }

    public static ReconciliationBatch Create(DateOnly reconciliationDate, string networkSource)
    {
        if (string.IsNullOrWhiteSpace(networkSource))
            throw new ArgumentException("Network source is required", nameof(networkSource));

        var batch = new ReconciliationBatch
        {
            Id = ReconciliationBatchId.New(),
            ReconciliationDate = reconciliationDate,
            NetworkSource = networkSource,
            Status = ReconciliationBatchStatus.Open
        };
        batch.RaiseDomainEvent(new ReconciliationBatchCreatedEvent(batch.Id, reconciliationDate, networkSource));
        return batch;
    }

    public void AddItem(string externalTransactionId, decimal externalAmount, string currency)
    {
        if (Status != ReconciliationBatchStatus.Open)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), "AddItem");

        if (_items.Any(i => i.ExternalTransactionId == externalTransactionId))
            throw new ArgumentException($"Item with ExternalTransactionId '{externalTransactionId}' already exists in this batch");

        _items.Add(ReconciliationItem.Create(Id, externalTransactionId, externalAmount, currency));
        MarkModified();
    }

    public void Process()
    {
        if (Status != ReconciliationBatchStatus.Open)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), ReconciliationBatchStatus.Processing.ToString());

        Status = ReconciliationBatchStatus.Processing;
        MarkModified();
    }

    public void MatchItem(string externalTransactionId, Guid internalTransactionId, decimal internalAmount)
    {
        if (Status != ReconciliationBatchStatus.Processing)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), "MatchItem");

        var item = _items.FirstOrDefault(i => i.ExternalTransactionId == externalTransactionId)
            ?? throw new ArgumentException($"Item '{externalTransactionId}' not found in batch");

        item.Match(internalTransactionId, internalAmount);

        if (item.Status == ReconciliationItemStatus.Disputed)
            RaiseDomainEvent(new ReconciliationDiscrepancyFoundEvent(
                Id, item.Id, externalTransactionId, item.DiscrepancyAmount));

        MarkModified();
    }

    public void MarkItemUnmatched(string externalTransactionId)
    {
        if (Status != ReconciliationBatchStatus.Processing)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), "MarkItemUnmatched");

        var item = _items.FirstOrDefault(i => i.ExternalTransactionId == externalTransactionId)
            ?? throw new ArgumentException($"Item '{externalTransactionId}' not found in batch");

        item.MarkUnmatched();
        MarkModified();
    }

    public void ResolveItem(string externalTransactionId, string note)
    {
        var item = _items.FirstOrDefault(i => i.ExternalTransactionId == externalTransactionId)
            ?? throw new ArgumentException($"Item '{externalTransactionId}' not found in batch");

        item.Resolve(note);
        MarkModified();
    }

    public void Complete()
    {
        if (Status != ReconciliationBatchStatus.Processing)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), ReconciliationBatchStatus.Completed.ToString());

        MatchedCount = _items.Count(i => i.Status == ReconciliationItemStatus.Matched);
        UnmatchedCount = _items.Count(i => i.Status == ReconciliationItemStatus.Unmatched);
        DisputedCount = _items.Count(i => i.Status == ReconciliationItemStatus.Disputed);

        Status = ReconciliationBatchStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new ReconciliationBatchCompletedEvent(Id, MatchedCount, UnmatchedCount, DisputedCount));
    }

    public void Fail(string reason)
    {
        if (Status == ReconciliationBatchStatus.Completed || Status == ReconciliationBatchStatus.Failed)
            throw new InvalidStateTransitionException("ReconciliationBatch", Status.ToString(), ReconciliationBatchStatus.Failed.ToString());

        Status = ReconciliationBatchStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
        MarkModified();
    }
}
