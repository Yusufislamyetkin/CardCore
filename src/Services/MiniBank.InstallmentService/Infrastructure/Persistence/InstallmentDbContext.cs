using Microsoft.EntityFrameworkCore;
using MiniBank.InstallmentService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.InstallmentService.Infrastructure.Persistence;

public sealed class InstallmentDbContext : BaseDbContext
{
    public InstallmentDbContext(DbContextOptions<InstallmentDbContext> options) : base(options) { }

    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<InstallmentItem> InstallmentItems => Set<InstallmentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InstallmentPlan>(entity =>
        {
            entity.ToTable("installment_plans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => InstallmentPlanId.From(raw));
            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, raw => CardId.From(raw)).IsRequired();
            entity.HasIndex(e => e.CardId).HasDatabaseName("ix_plans_card_id");
            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, raw => AccountId.From(raw)).IsRequired();
            entity.Property(e => e.TransactionId)
                .HasConversion(id => id.Value, raw => TransactionId.From(raw)).IsRequired();
            entity.HasIndex(e => e.TransactionId).IsUnique().HasDatabaseName("ix_plans_transaction_id");
            entity.OwnsOne(e => e.TotalAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("total_amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.OwnsOne(e => e.InstallmentAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("installment_amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("installment_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.InstallmentCount).IsRequired();
            entity.Property(e => e.PaidCount).IsRequired();
            entity.Property(e => e.InterestRate).HasPrecision(5, 4).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
            entity.Ignore(e => e.DomainEvents);
            entity.Metadata.FindNavigation(nameof(InstallmentPlan.Items))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<InstallmentItem>(entity =>
        {
            entity.ToTable("installment_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlanId)
                .HasConversion(id => id.Value, raw => InstallmentPlanId.From(raw)).IsRequired();
            entity.HasIndex(e => e.PlanId).HasDatabaseName("ix_items_plan_id");
            entity.Property(e => e.InstallmentNumber).IsRequired();
            entity.OwnsOne(e => e.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("amount_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.DueDate).IsRequired();
            entity.Property(e => e.PaidAt);
            entity.Property(e => e.Status)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
        });
    }
}
