using MiniBank.EodService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.EodService.Domain.Entities;

public enum EodRunStatus { Scheduled = 1, Running = 2, Completed = 3, Failed = 4 }

public sealed class EodRun : AggregateRoot<EodRunId>
{
    public DateOnly ProcessingDate { get; private set; }
    public EodRunStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int ProcessedTransactionCount { get; private set; }
    public decimal TotalSettledAmount { get; private set; }
    public string? ErrorMessage { get; private set; }

    private EodRun() { }

    public static EodRun Schedule(DateOnly processingDate)
    {
        var run = new EodRun
        {
            Id = EodRunId.New(),
            ProcessingDate = processingDate,
            Status = EodRunStatus.Scheduled
        };
        run.RaiseDomainEvent(new EodRunScheduledEvent(run.Id, processingDate));
        return run;
    }

    public void Start()
    {
        if (Status != EodRunStatus.Scheduled)
            throw new InvalidStateTransitionException("EodRun", Status.ToString(), EodRunStatus.Running.ToString());

        Status = EodRunStatus.Running;
        StartedAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new EodRunStartedEvent(Id, ProcessingDate, StartedAt.Value));
    }

    public void Complete(int processedTransactionCount, decimal totalSettledAmount)
    {
        if (Status != EodRunStatus.Running)
            throw new InvalidStateTransitionException("EodRun", Status.ToString(), EodRunStatus.Completed.ToString());

        Status = EodRunStatus.Completed;
        ProcessedTransactionCount = processedTransactionCount;
        TotalSettledAmount = totalSettledAmount;
        CompletedAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new EodRunCompletedEvent(Id, ProcessingDate, processedTransactionCount, totalSettledAmount));
    }

    public void Fail(string errorMessage)
    {
        if (Status != EodRunStatus.Running)
            throw new InvalidStateTransitionException("EodRun", Status.ToString(), EodRunStatus.Failed.ToString());

        Status = EodRunStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        MarkModified();
        RaiseDomainEvent(new EodRunFailedEvent(Id, ProcessingDate, errorMessage));
    }
}
