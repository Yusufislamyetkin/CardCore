using Microsoft.EntityFrameworkCore;
using MiniBank.NotificationService.Domain.Entities;
using MiniBank.Shared.Domain.Primitives;
using MiniBank.Shared.Infrastructure.Persistence;
using Notification = MiniBank.NotificationService.Domain.Entities.Notification;

namespace MiniBank.NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext : BaseDbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(id => id.Value, v => NotificationId.From(v))
                .HasColumnName("id");

            entity.Property(e => e.RecipientId)
                .HasConversion(id => id.Value, v => UserId.From(v))
                .HasColumnName("recipient_id");

            entity.Property(e => e.RecipientAddress).HasColumnName("recipient_address").HasMaxLength(500).IsRequired();

            entity.Property(e => e.Channel)
                .HasConversion(c => c.ToString(), v => Enum.Parse<NotificationChannel>(v))
                .HasColumnName("channel");

            entity.Property(e => e.Type)
                .HasConversion(t => t.ToString(), v => Enum.Parse<NotificationType>(v))
                .HasColumnName("type");

            entity.Property(e => e.Subject).HasColumnName("subject").HasMaxLength(300);
            entity.Property(e => e.Body).HasColumnName("body").IsRequired();

            entity.Property(e => e.Status)
                .HasConversion(s => s.ToString(), v => Enum.Parse<NotificationStatus>(v))
                .HasColumnName("status");

            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(e => e.FailedAt).HasColumnName("failed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.RecipientId).HasDatabaseName("ix_notifications_recipient_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_notifications_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_notifications_created_at");
        });
    }
}
