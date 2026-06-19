using Microsoft.EntityFrameworkCore;
using MiniBank.FraudService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.FraudService.Infrastructure.Persistence;

public sealed class FraudDbContext : BaseDbContext
{
    public FraudDbContext(DbContextOptions<FraudDbContext> options) : base(options) { }

    public DbSet<FraudCase> FraudCases => Set<FraudCase>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FraudCase>(entity =>
        {
            entity.ToTable("fraud_cases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => FraudCaseId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.TransactionId)
                .HasConversion(id => id.Value, v => TransactionId.From(v))
                .HasColumnName("transaction_id");

            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, v => CardId.From(v))
                .HasColumnName("card_id");

            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, v => AccountId.From(v))
                .HasColumnName("account_id");

            entity.OwnsOne(e => e.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(18,4)");
                money.Property(m => m.Currency).HasColumnName("currency")
                    .HasConversion(c => c.ToString(), v => Enum.Parse<Currency>(v));
            });

            entity.Property(e => e.RiskScore)
                .HasColumnName("risk_score")
                .HasColumnType("numeric(5,2)");

            entity.Property(e => e.Status)
                .HasConversion(s => s.ToString(), v => Enum.Parse<FraudCaseStatus>(v))
                .HasColumnName("status");

            entity.Property(e => e.Resolution)
                .HasConversion(r => r.HasValue ? r.Value.ToString() : null,
                    v => v != null ? Enum.Parse<FraudResolution>(v) : (FraudResolution?)null)
                .HasColumnName("resolution");

            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(1000);
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(200);
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.TransactionId).HasDatabaseName("ix_fraud_cases_transaction_id");
            entity.HasIndex(e => e.CardId).HasDatabaseName("ix_fraud_cases_card_id");

            entity.HasMany(e => e.Flags)
                .WithOne()
                .HasForeignKey(f => f.CaseId)
                .HasPrincipalKey(e => e.Id);

            entity.Navigation(e => e.Flags)
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasField("_flags");
        });

        modelBuilder.Entity<FraudFlag>(entity =>
        {
            entity.ToTable("fraud_flags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => FraudFlagId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.CaseId)
                .HasConversion(id => id.Value, v => FraudCaseId.From(v))
                .HasColumnName("case_id");

            entity.Property(e => e.RuleName).HasColumnName("rule_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            entity.Property(e => e.ScoreContribution)
                .HasColumnName("score_contribution")
                .HasColumnType("numeric(5,2)");
        });
    }
}
