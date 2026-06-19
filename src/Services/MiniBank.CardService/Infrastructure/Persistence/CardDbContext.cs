using Microsoft.EntityFrameworkCore;
using MiniBank.CardService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Domain.ValueObjects;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.CardService.Infrastructure.Persistence;

public sealed class CardDbContext : BaseDbContext
{
    public CardDbContext(DbContextOptions<CardDbContext> options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardLimit> CardLimits => Set<CardLimit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Card>(entity =>
        {
            entity.ToTable("cards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => CardId.From(raw));
            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, raw => AccountId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.AccountId).HasDatabaseName("ix_cards_account_id");
            entity.Property(e => e.CustomerId)
                .HasConversion(id => id.Value, raw => CustomerId.From(raw))
                .IsRequired();
            entity.Property(e => e.PanHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.PanHash).HasDatabaseName("ix_cards_pan_hash");
            entity.Property(e => e.PanMasked).HasMaxLength(25).IsRequired();
            entity.Property(e => e.Last4).HasMaxLength(4).IsRequired();
            entity.Property(e => e.Bin).HasMaxLength(8).IsRequired();
            entity.OwnsOne(e => e.Expiry, exp =>
            {
                exp.Property(x => x.Month).HasColumnName("expiry_month");
                exp.Property(x => x.Year).HasColumnName("expiry_year");
            });
            entity.Property(e => e.CardType)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.BlockReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
            entity.Ignore(e => e.DomainEvents);
            entity.Metadata.FindNavigation(nameof(Card.Limits))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CardLimit>(entity =>
        {
            entity.ToTable("card_limits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, raw => CardId.From(raw))
                .IsRequired();
            entity.HasIndex(e => new { e.CardId, e.LimitType })
                .IsUnique()
                .HasDatabaseName("ix_card_limits_card_type");
            entity.Property(e => e.LimitType)
                .HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.OwnsOne(e => e.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("limit_amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("limit_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }
}
