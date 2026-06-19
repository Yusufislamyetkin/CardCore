using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MiniBank.Shared.Infrastructure.Persistence;

public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options) { }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.OccurredAt).IsRequired();
            entity.HasIndex(e => e.ProcessedAt)
                .HasFilter("processed_at IS NULL")
                .HasDatabaseName("ix_outbox_unprocessed");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries()
            .Where(e => e.Entity is IAggregateWithEvents)
            .Select(e => (IAggregateWithEvents)e.Entity)
            .ToList();

        var outboxMessages = new List<OutboxMessage>();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.GetDomainEvents())
            {
                outboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = DateTime.UtcNow
                });
            }
            aggregate.ClearEvents();
        }

        if (outboxMessages.Count > 0)
            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);

        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}
