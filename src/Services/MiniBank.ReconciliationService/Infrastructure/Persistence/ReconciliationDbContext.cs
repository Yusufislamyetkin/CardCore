using Microsoft.EntityFrameworkCore;
using MiniBank.ReconciliationService.Domain.Entities;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.ReconciliationService.Infrastructure.Persistence;

public sealed class ReconciliationDbContext : BaseDbContext
{
    public ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : base(options) { }

    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReconciliationBatch>(entity =>
        {
            entity.ToTable("reconciliation_batches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => ReconciliationBatchId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.ReconciliationDate).HasColumnName("reconciliation_date");
            entity.Property(e => e.NetworkSource).HasColumnName("network_source").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion(s => s.ToString(), v => Enum.Parse<ReconciliationBatchStatus>(v))
                .HasColumnName("status");
            entity.Property(e => e.MatchedCount).HasColumnName("matched_count");
            entity.Property(e => e.UnmatchedCount).HasColumnName("unmatched_count");
            entity.Property(e => e.DisputedCount).HasColumnName("disputed_count");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.ReconciliationDate, e.NetworkSource })
                .IsUnique()
                .HasDatabaseName("ix_recon_batches_date_source");

            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey(i => i.BatchId)
                .HasPrincipalKey(e => e.Id);

            entity.Navigation(e => e.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasField("_items");
        });

        modelBuilder.Entity<ReconciliationItem>(entity =>
        {
            entity.ToTable("reconciliation_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => ReconciliationItemId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.BatchId)
                .HasConversion(id => id.Value, v => ReconciliationBatchId.From(v))
                .HasColumnName("batch_id");

            entity.Property(e => e.ExternalTransactionId).HasColumnName("external_transaction_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.InternalTransactionId).HasColumnName("internal_transaction_id");
            entity.Property(e => e.ExternalAmount).HasColumnName("external_amount").HasColumnType("numeric(18,4)");
            entity.Property(e => e.InternalAmount).HasColumnName("internal_amount").HasColumnType("numeric(18,4)");
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(e => e.DiscrepancyAmount).HasColumnName("discrepancy_amount").HasColumnType("numeric(18,4)");
            entity.Property(e => e.Status)
                .HasConversion(s => s.ToString(), v => Enum.Parse<ReconciliationItemStatus>(v))
                .HasColumnName("status");
            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.BatchId, e.ExternalTransactionId })
                .IsUnique()
                .HasDatabaseName("ix_recon_items_batch_external");
        });
    }
}
