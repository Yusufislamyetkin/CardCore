using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.Shared.Infrastructure.Messaging;

public sealed class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : BaseDbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started for {DbContext}.", typeof(TDbContext).Name);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOutboxMessages(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "OutboxProcessor error."); }
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0) return;
        _logger.LogInformation("Processing {Count} outbox messages.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                // TODO: Faz 2'de Kafka publisher eklenecek
                message.ProcessedAt = DateTime.UtcNow;
                _logger.LogInformation("Outbox message {Id} of type {Type} processed.", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                _logger.LogWarning("Outbox message {Id} failed (attempt {Attempt}): {Error}", message.Id, message.RetryCount, ex.Message);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
