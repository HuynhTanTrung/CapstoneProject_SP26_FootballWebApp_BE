using Microsoft.EntityFrameworkCore;

namespace VNFootballLeagues.Repositories.Models;

public partial class VNFootballLeaguesDBContext
{
    public virtual DbSet<SePayWebhookLog> SePayWebhookLogs { get; set; }

    public virtual DbSet<SubscriptionPayment> SubscriptionPayments { get; set; }

    public virtual DbSet<UserSubscription> UserSubscriptions { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("UserSubscription");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.PlanCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.PlanName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasIndex(e => e.Status, "IX_UserSubscription_Status");
            entity.HasIndex(e => e.ExpiresAt, "IX_UserSubscription_ExpiresAt");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserSubscription_User");
        });

        modelBuilder.Entity<SubscriptionPayment>(entity =>
        {
            entity.HasKey(e => e.PaymentId);

            entity.ToTable("SubscriptionPayment");

            entity.Property(e => e.PaymentId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.PlanCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.PlanName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.PaymentCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Provider)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(e => e.BankCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.AccountNumber)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.AccountName)
                .IsRequired()
                .HasMaxLength(150);
            entity.Property(e => e.TransferContent)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.QrUrl)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.ManualUpdatedByName).HasMaxLength(150);
            entity.Property(e => e.ManualUpdateReason).HasMaxLength(500);
            entity.Property(e => e.SePayReferenceCode).HasMaxLength(255);
            entity.Property(e => e.Gateway).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasIndex(e => e.PaymentCode, "IX_SubscriptionPayment_PaymentCode").IsUnique();
            entity.HasIndex(e => e.UserId, "IX_SubscriptionPayment_UserId");
            entity.HasIndex(e => e.Status, "IX_SubscriptionPayment_Status");
            entity.HasIndex(e => e.ExpiresAt, "IX_SubscriptionPayment_ExpiresAt");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_SubscriptionPayment_User");
        });

        modelBuilder.Entity<SePayWebhookLog>(entity =>
        {
            entity.HasKey(e => e.WebhookLogId);

            entity.ToTable("SePayWebhookLog");

            entity.Property(e => e.WebhookLogId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.PaymentCode).HasMaxLength(50);
            entity.Property(e => e.ReferenceCode).HasMaxLength(255);
            entity.Property(e => e.TransferType)
                .IsRequired()
                .HasMaxLength(10);
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.ProcessingStatus)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(e => e.ProcessingMessage).HasMaxLength(255);
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasIndex(e => e.SePayTransactionId, "IX_SePayWebhookLog_SePayTransactionId").IsUnique();
            entity.HasIndex(e => e.ProcessingStatus, "IX_SePayWebhookLog_ProcessingStatus");
            entity.HasIndex(e => e.ReceivedAt, "IX_SePayWebhookLog_ReceivedAt");
        });
    }
}
