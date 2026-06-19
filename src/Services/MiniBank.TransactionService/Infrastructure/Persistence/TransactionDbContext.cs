using Microsoft.EntityFrameworkCore;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;
using MiniBank.TransactionService.Domain.Entities;

namespace MiniBank.TransactionService.Infrastructure.Persistence;

public sealed class TransactionDbContext : BaseDbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => TransactionId.From(raw));
            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, raw => CardId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.CardId).HasDatabaseName("ix_transactions_card_id");
            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, raw => AccountId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.AccountId).HasDatabaseName("ix_transactions_account_id");
            entity.Property(e => e.AuthorizationId).HasMaxLength(36);
            entity.HasIndex(e => e.AuthorizationId).HasDatabaseName("ix_transactions_auth_id");
            entity.Property(e => e.AuthorizationCode).HasMaxLength(20);
            entity.OwnsOne(e => e.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("amount_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.TransactionType)
                .HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_transactions_status");
            entity.Property(e => e.MerchantId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MerchantName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReferenceId).HasMaxLength(36);
            entity.Property(e => e.ReverseReason).HasMaxLength(500);
            entity.Property(e => e.SettledAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
            entity.Ignore(e => e.DomainEvents);
        });
    }
}
