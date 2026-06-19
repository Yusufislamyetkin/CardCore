using Microsoft.EntityFrameworkCore;
using MiniBank.IamService.Domain.Entities;
using MiniBank.IamService.Domain.Enums;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;

namespace MiniBank.IamService.Infrastructure.Persistence;

public sealed class IamDbContext : BaseDbContext
{
    public IamDbContext(DbContextOptions<IamDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, raw => UserId.From(raw));
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("ix_users_email");
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.FailedLoginCount).IsRequired();
            entity.Property(e => e.LockedUntil);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId)
                .HasConversion(id => id.Value, raw => UserId.From(raw))
                .IsRequired();
            entity.Property(e => e.Token).HasMaxLength(512).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique().HasDatabaseName("ix_refresh_tokens_token");
            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.RevokedAt);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(512);
        });
    }
}
