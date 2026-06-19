using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.NotificationService.Api.Dtos;
using MiniBank.NotificationService.Domain.Entities;
using MiniBank.NotificationService.Infrastructure.Persistence;
using MiniBank.Shared.Domain.Primitives;
using Notification = MiniBank.NotificationService.Domain.Entities.Notification;

namespace MiniBank.NotificationService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public sealed class NotificationController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public NotificationController(NotificationDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Queue([FromBody] QueueNotificationRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.RecipientId, out var recipientGuid))
            return BadRequest(new { error = "Invalid RecipientId" });

        if (!Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
            return BadRequest(new { error = $"Invalid channel: {request.Channel}. Use Email, Sms, or Push." });

        if (!Enum.TryParse<NotificationType>(request.Type, true, out var type))
            return BadRequest(new { error = $"Invalid type: {request.Type}" });

        var notification = Notification.Queue(
            UserId.From(recipientGuid), request.RecipientAddress,
            channel, type, request.Subject, request.Body);

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = notification.Id.Value }, ToResponse(notification));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var n = await _db.Notifications.FindAsync(NotificationId.From(id), ct);
        return n is null ? NotFound() : Ok(ToResponse(n));
    }

    [HttpGet("recipient/{recipientId:guid}")]
    public async Task<IActionResult> GetByRecipient(
        Guid recipientId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var list = await _db.Notifications
            .Where(n => n.RecipientId == UserId.From(recipientId))
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        return Ok(list.Select(ToResponse));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var list = await _db.Notifications
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        return Ok(list.Select(ToResponse));
    }

    [HttpPut("{id:guid}/sent")]
    public async Task<IActionResult> MarkSent(Guid id, CancellationToken ct)
    {
        var n = await _db.Notifications.FindAsync(NotificationId.From(id), ct);
        if (n is null) return NotFound();

        n.MarkSent();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(n));
    }

    [HttpPut("{id:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid id, CancellationToken ct)
    {
        var n = await _db.Notifications.FindAsync(NotificationId.From(id), ct);
        if (n is null) return NotFound();

        n.MarkDelivered();
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(n));
    }

    [HttpPut("{id:guid}/failed")]
    public async Task<IActionResult> MarkFailed(Guid id, [FromBody] MarkFailedRequest request, CancellationToken ct)
    {
        var n = await _db.Notifications.FindAsync(NotificationId.From(id), ct);
        if (n is null) return NotFound();

        n.MarkFailed(request.Reason);
        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(n));
    }

    private static NotificationResponse ToResponse(Notification n) => new(
        n.Id.Value, n.RecipientId.Value, n.RecipientAddress,
        n.Channel.ToString(), n.Type.ToString(),
        n.Subject, n.Body, n.Status.ToString(),
        n.RetryCount, n.FailureReason,
        n.SentAt, n.DeliveredAt, n.FailedAt,
        n.CreatedAt, n.CanRetry);
}
