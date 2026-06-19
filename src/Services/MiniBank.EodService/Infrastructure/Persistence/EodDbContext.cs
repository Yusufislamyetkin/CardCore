using Microsoft.EntityFrameworkCore;
using MiniBank.EodService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.EodService.Infrastructure.Persistence;

public sealed class EodDbContext : BaseDbContext
{
    public EodDbContext(DbContextOptions<EodDbContext> options) : base(options) { }

    public DbSet<EodRun> EodRuns => Set<EodRun>();
    public DbSet<Statement> Statements => Set<Statement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EodRun>(entity =>
        {
            entity.ToTable("eod_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => EodRunId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.ProcessingDate).HasColumnName("processing_date");
            entity.Property(e => e.Status)
                .HasConversion(s => s.ToString(), v => Enum.Parse<EodRunStatus>(v))
                .HasColumnName("status");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ProcessedTransactionCount).HasColumnName("processed_transaction_count");
            entity.Property(e => e.TotalSettledAmount)
                .HasColumnName("total_settled_amount")
                .HasColumnType("numeric(18,4)");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.ProcessingDate)
                .IsUnique()
                .HasDatabaseName("ix_eod_runs_processing_date");
        });

        modelBuilder.Entity<Statement>(entity =>
        {
            entity.ToTable("statements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => StatementId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, v => AccountId.From(v))
                .HasColumnName("account_id");

            entity.Property(e => e.GeneratedByRunId)
                .HasConversion(id => id.Value, v => EodRunId.From(v))
                .HasColumnName("generated_by_run_id");

            entity.Property(e => e.Year).HasColumnName("year");
            entity.Property(e => e.Month).HasColumnName("month");
            entity.Property(e => e.TransactionCount).HasColumnName("transaction_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.OwnsOne(e => e.OpeningBalance, m =>
            {
                m.Property(x => x.Amount).HasColumnName("opening_balance_amount").HasColumnType("numeric(18,4)");
                m.Property(x => x.Currency).HasColumnName("opening_balance_currency")
                    .HasConversion(c => c.ToString(), v => Enum.Parse<Currency>(v));
            });
            entity.OwnsOne(e => e.ClosingBalance, m =>
            {
                m.Property(x => x.Amount).HasColumnName("closing_balance_amount").HasColumnType("numeric(18,4)");
                m.Property(x => x.Currency).HasColumnName("closing_balance_currency")
                    .HasConversion(c => c.ToString(), v => Enum.Parse<Currency>(v));
            });
            entity.OwnsOne(e => e.TotalDebits, m =>
            {
                m.Property(x => x.Amount).HasColumnName("total_debits_amount").HasColumnType("numeric(18,4)");
                m.Property(x => x.Currency).HasColumnName("total_debits_currency")
                    .HasConversion(c => c.ToString(), v => Enum.Parse<Currency>(v));
            });
            entity.OwnsOne(e => e.TotalCredits, m =>
            {
                m.Property(x => x.Amount).HasColumnName("total_credits_amount").HasColumnType("numeric(18,4)");
                m.Property(x => x.Currency).HasColumnName("total_credits_currency")
                    .HasConversion(c => c.ToString(), v => Enum.Parse<Currency>(v));
            });

            entity.HasIndex(e => new { e.AccountId, e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("ix_statements_account_period");
        });
    }
}
