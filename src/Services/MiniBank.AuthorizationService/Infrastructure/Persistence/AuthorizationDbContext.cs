using Microsoft.EntityFrameworkCore;
using MiniBank.AuthorizationService.Domain.Entities;
using MiniBank.Shared.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.AuthorizationService.Infrastructure.Persistence;

public sealed class AuthorizationDbContext : BaseDbContext
{
    public AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options) : base(options) { }

    public DbSet<Authorization> Authorizations => Set<Authorization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Authorization>(entity =>
        {
            entity.ToTable("authorizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => AuthorizationId.From(raw));
            entity.Property(e => e.CardId)
                .HasConversion(id => id.Value, raw => CardId.From(raw))
                .IsRequired();
            entity.HasIndex(e => e.CardId).HasDatabaseName("ix_authorizations_card_id");
            entity.Property(e => e.AccountId)
                .HasConversion(id => id.Value, raw => AccountId.From(raw))
                .IsRequired();
            entity.Property(e => e.PanHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.PanHash).HasDatabaseName("ix_authorizations_pan_hash");
            entity.Property(e => e.MerchantId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MerchantName).HasMaxLength(200).IsRequired();
            entity.OwnsOne(e => e.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
                m.Property(x => x.Currency).HasColumnName("amount_currency")
                    .HasConversion<string>().HasMaxLength(10);
            });
            entity.Property(e => e.Status)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.DeclineReason)
                .HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.AuthorizationCode).HasMaxLength(20);
            entity.Property(e => e.ReverseReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
            entity.Ignore(e => e.DomainEvents);
        });
    }
}
