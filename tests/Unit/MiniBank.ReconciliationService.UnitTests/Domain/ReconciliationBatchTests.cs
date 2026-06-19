using FluentAssertions;
using MiniBank.ReconciliationService.Domain.Entities;
using MiniBank.ReconciliationService.Domain.Events;
using MiniBank.Shared.Domain.Exceptions;
using Xunit;

namespace MiniBank.ReconciliationService.UnitTests.Domain;

public sealed class ReconciliationBatchTests
{
    private static readonly DateOnly TestDate = new(2026, 6, 19);
    private const string TestSource = "VISA_NETWORK";

    private static ReconciliationBatch CreateBatch() =>
        ReconciliationBatch.Create(TestDate, TestSource);

    [Fact]
    public void Create_ShouldSetStatusOpen()
    {
        var batch = CreateBatch();

        batch.Status.Should().Be(ReconciliationBatchStatus.Open);
        batch.ReconciliationDate.Should().Be(TestDate);
        batch.NetworkSource.Should().Be(TestSource);
    }

    [Fact]
    public void Create_ShouldRaiseBatchCreatedEvent()
    {
        var batch = CreateBatch();

        batch.DomainEvents.Should().ContainSingle(e => e is ReconciliationBatchCreatedEvent);
    }

    [Fact]
    public void Create_ShouldThrow_WhenNetworkSourceEmpty()
    {
        var act = () => ReconciliationBatch.Create(TestDate, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_ShouldAddItemToBatch()
    {
        var batch = CreateBatch();

        batch.AddItem("EXT-001", 100m, "TRY");

        batch.Items.Should().HaveCount(1);
        batch.Items.First().ExternalTransactionId.Should().Be("EXT-001");
        batch.Items.First().ExternalAmount.Should().Be(100m);
    }

    [Fact]
    public void AddItem_ShouldThrow_WhenDuplicateExternalId()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");

        var act = () => batch.AddItem("EXT-001", 200m, "TRY");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_ShouldThrow_WhenBatchNotOpen()
    {
        var batch = CreateBatch();
        batch.Process();

        var act = () => batch.AddItem("EXT-001", 100m, "TRY");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Process_ShouldTransitionToProcessing()
    {
        var batch = CreateBatch();

        batch.Process();

        batch.Status.Should().Be(ReconciliationBatchStatus.Processing);
    }

    [Fact]
    public void MatchItem_ShouldMarkItemAsMatched_WhenAmountsEqual()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");
        batch.Process();
        var internalId = Guid.NewGuid();

        batch.MatchItem("EXT-001", internalId, 100m);

        var item = batch.Items.First();
        item.Status.Should().Be(ReconciliationItemStatus.Matched);
        item.DiscrepancyAmount.Should().Be(0m);
        item.InternalTransactionId.Should().Be(internalId);
    }

    [Fact]
    public void MatchItem_ShouldMarkItemAsDisputed_WhenAmountsDiffer()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");
        batch.Process();

        batch.MatchItem("EXT-001", Guid.NewGuid(), 95m);

        var item = batch.Items.First();
        item.Status.Should().Be(ReconciliationItemStatus.Disputed);
        item.DiscrepancyAmount.Should().Be(5m);
        batch.DomainEvents.Should().Contain(e => e is ReconciliationDiscrepancyFoundEvent);
    }

    [Fact]
    public void MarkItemUnmatched_ShouldSetStatusUnmatched()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");
        batch.Process();

        batch.MarkItemUnmatched("EXT-001");

        var item = batch.Items.First();
        item.Status.Should().Be(ReconciliationItemStatus.Unmatched);
        item.DiscrepancyAmount.Should().Be(100m);
    }

    [Fact]
    public void Complete_ShouldCalculateCounts()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");
        batch.AddItem("EXT-002", 200m, "TRY");
        batch.AddItem("EXT-003", 300m, "TRY");
        batch.Process();
        batch.MatchItem("EXT-001", Guid.NewGuid(), 100m);   // matched
        batch.MatchItem("EXT-002", Guid.NewGuid(), 195m);   // disputed
        batch.MarkItemUnmatched("EXT-003");                 // unmatched

        batch.Complete();

        batch.Status.Should().Be(ReconciliationBatchStatus.Completed);
        batch.MatchedCount.Should().Be(1);
        batch.UnmatchedCount.Should().Be(1);
        batch.DisputedCount.Should().Be(1);
        batch.CompletedAt.Should().NotBeNull();
        batch.DomainEvents.Should().Contain(e => e is ReconciliationBatchCompletedEvent);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenNotProcessing()
    {
        var batch = CreateBatch();

        var act = () => batch.Complete();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Fail_ShouldTransitionToFailed()
    {
        var batch = CreateBatch();
        batch.Process();

        batch.Fail("Network file corrupted");

        batch.Status.Should().Be(ReconciliationBatchStatus.Failed);
        batch.FailureReason.Should().Be("Network file corrupted");
    }

    [Fact]
    public void Fail_ShouldThrow_WhenAlreadyCompleted()
    {
        var batch = CreateBatch();
        batch.Process();
        batch.Complete();

        var act = () => batch.Fail("late failure");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void ResolveItem_ShouldMarkItemAsManuallyResolved()
    {
        var batch = CreateBatch();
        batch.AddItem("EXT-001", 100m, "TRY");
        batch.Process();
        batch.MatchItem("EXT-001", Guid.NewGuid(), 95m);

        batch.ResolveItem("EXT-001", "Approved by ops team — currency rounding");

        var item = batch.Items.First();
        item.Status.Should().Be(ReconciliationItemStatus.ManuallyResolved);
        item.ResolutionNote.Should().Contain("currency rounding");
    }
}
