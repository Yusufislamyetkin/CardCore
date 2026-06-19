using Microsoft.EntityFrameworkCore;
using MiniBank.AccountService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.AccountService.Infrastructure.Persistence;

public sealed class AccountDbContext : BaseDbContext
{
    public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => AccountId.From(raw));
            entity.Property(e => e.CustomerId)
                .HasConversion(id => id.Value, raw => CustomerId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("ix_accounts_customer_id");
            entity.Property(e => e.Iban)
                .HasConversion(iban => iban.Value, raw => Iban.Create(raw))
                .HasMaxLength(34)
                .IsRequired();
            entity.HasIndex(e => e.Iban).IsUnique().HasDatabaseName("ix_accounts_iban");
            entity.OwnsOne(e => e.Balance, balance =>
            {
                balance.Property(m => m.Amount).HasColumnName("balance_amount").HasPrecision(18, 4);
                balance.Property(m => m.Currency)
                    .HasColumnName("balance_currency")
                    .HasConversion<string>()
                    .HasMaxLength(10);
            });
            entity.Property(e => e.AccountType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
            entity.Ignore(e => e.DomainEvents);
            entity.Metadata.FindNavigation(nameof(Account.LedgerEntries))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("ledger_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => LedgerEntryId.From(raw));
            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, raw => AccountId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.AccountId).HasDatabaseName("ix_ledger_entries_account_id");
            entity.Property(e => e.EntryType)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();
            entity.OwnsOne(e => e.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("amount_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.OwnsOne(e => e.BalanceAfter, m =>
            {
                m.Property(x => x.Amount).HasColumnName("balance_after_amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("balance_after_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.TransactionRef).HasMaxLength(100);
            entity.Property(e => e.OccurredAt).IsRequired();
        });
    }
}
