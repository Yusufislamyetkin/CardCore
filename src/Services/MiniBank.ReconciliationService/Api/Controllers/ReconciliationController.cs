using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.ReconciliationService.Api.Dtos;
using MiniBank.ReconciliationService.Domain.Entities;
using MiniBank.ReconciliationService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Primitives;

namespace MiniBank.ReconciliationService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reconciliation")]
public sealed class ReconciliationController : ControllerBase
{
    private readonly ReconciliationDbContext _db;

    public ReconciliationController(ReconciliationDbContext db) => _db = db;

    [HttpPost("batches")]
    public async Task<IActionResult> Create([FromBody] CreateBatchRequest request, CancellationToken ct)
    {
        if (await _db.ReconciliationBatches.AnyAsync(b =>
            b.ReconciliationDate == request.ReconciliationDate &&
            b.NetworkSource == request.NetworkSource, ct))
            return Conflict(new { error = $"Batch for {request.ReconciliationDate} / {request.NetworkSource} already exists" });

        var batch = ReconciliationBatch.Create(request.ReconciliationDate, request.NetworkSource);
        _db.ReconciliationBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = batch.Id.Value }, ToResponse(batch));
    }

    [HttpGet("batches/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        return batch is null ? NotFound() : Ok(ToResponse(batch));
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetByDate([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var query = _db.ReconciliationBatches.Include(b => b.Items).AsQueryable();
        if (date.HasValue)
            query = query.Where(b => b.ReconciliationDate == date.Value);

        var batches = await query.OrderByDescending(b => b.ReconciliationDate).Take(50).ToListAsync(ct);
        return Ok(batches.Select(ToResponse));
    }

    [HttpPost("batches/{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemRequest request, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.AddItem(request.ExternalTransactionId, request.ExternalAmount, request.Currency);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches.FindAsync(ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.Process();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/match")]
    public async Task<IActionResult> Match(Guid id, [FromBody] MatchItemRequest request, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.MatchItem(request.ExternalTransactionId, request.InternalTransactionId, request.InternalAmount);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/unmatch")]
    public async Task<IActionResult> Unmatch(Guid id, [FromBody] MarkUnmatchedRequest request, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.MarkItemUnmatched(request.ExternalTransactionId);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveItemRequest request, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.ResolveItem(request.ExternalTransactionId, request.Note);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.Complete();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    [HttpPut("batches/{id:guid}/fail")]
    public async Task<IActionResult> Fail(Guid id, [FromBody] FailBatchRequest request, CancellationToken ct)
    {
        var batch = await _db.ReconciliationBatches.FindAsync(ReconciliationBatchId.From(id), ct);
        if (batch is null) return NotFound();

        batch.Fail(request.Reason);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(batch));
    }

    private static ReconciliationBatchResponse ToResponse(ReconciliationBatch b) => new(
        b.Id.Value, b.ReconciliationDate, b.NetworkSource, b.Status.ToString(),
        b.Items.Count, b.MatchedCount, b.UnmatchedCount, b.DisputedCount,
        b.CompletedAt, b.FailureReason, b.CreatedAt,
        b.Items.Select(i => new ReconciliationItemResponse(
            i.Id.Value, i.ExternalTransactionId, i.InternalTransactionId,
            i.ExternalAmount, i.InternalAmount, i.Currency,
            i.DiscrepancyAmount, i.Status.ToString(), i.ResolutionNote)));
}
