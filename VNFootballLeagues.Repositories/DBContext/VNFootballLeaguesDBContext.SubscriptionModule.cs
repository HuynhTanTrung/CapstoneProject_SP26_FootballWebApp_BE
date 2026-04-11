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

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(e => e.PredictionId);

            entity.ToTable("Predictions");

            entity.Property(e => e.PredictionId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User)
                .WithMany(p => p.Predictions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Match)
                .WithMany(p => p.Predictions)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reward>(entity =>
        {
            entity.HasKey(e => e.RewardId);

            entity.ToTable("Reward");

            entity.Property(e => e.RewardId).ValueGeneratedOnAdd();
            entity.Property(e => e.RewardName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IconUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<UserReward>(entity =>
        {
            entity.HasKey(e => e.UserRewardId);

            entity.ToTable("UserReward");

            entity.Property(e => e.UserRewardId).ValueGeneratedOnAdd();

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserRewards)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Reward)
                .WithMany(p => p.UserRewards)
                .HasForeignKey(d => d.RewardId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserPredictionStats>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("UserPredictionStats");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.LastUpdated).HasColumnType("datetime");

            entity.HasOne(d => d.User)
                .WithOne(p => p.UserPredictionStats)
                .HasForeignKey<UserPredictionStats>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyCheckIn>(entity =>
        {
            entity.HasKey(e => e.CheckInId);
            entity.ToTable("DailyCheckIn");
            entity.Property(e => e.CheckInId).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckInDate).HasColumnType("date");
            entity.HasIndex(e => new { e.UserId, e.CheckInDate }).IsUnique();
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyChatUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("DailyChatUsage");
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UsageDate).HasColumnType("date");
            entity.HasIndex(e => new { e.UserId, e.UsageDate }).IsUnique();
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId);
            entity.ToTable("Notification");
            entity.Property(e => e.NotificationId).ValueGeneratedOnAdd();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId);
            entity.ToTable("Notification");
            entity.Property(e => e.NotificationId).ValueGeneratedOnAdd();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ForumPost>(entity =>
        {
            entity.HasKey(e => e.PostId);
            entity.ToTable("ForumPost");
            entity.Property(e => e.PostId).ValueGeneratedOnAdd();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.LeagueTag).HasMaxLength(50);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ForumComment>(entity =>
        {
            entity.HasKey(e => e.CommentId);
            entity.ToTable("ForumComment");
            entity.Property(e => e.CommentId).ValueGeneratedOnAdd();
            entity.Property(e => e.Content).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
            entity.HasOne(d => d.Post).WithMany(p => p.Comments).HasForeignKey(d => d.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.ParentComment).WithMany().HasForeignKey(d => d.ParentCommentId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ForumReaction>(entity =>
        {
            entity.HasKey(e => e.ReactionId);
            entity.ToTable("ForumReaction");
            entity.Property(e => e.ReactionId).ValueGeneratedOnAdd();
            entity.Property(e => e.ReactionType).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => new { e.PostId, e.UserId }).IsUnique();
            entity.HasOne(d => d.Post).WithMany().HasForeignKey(d => d.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserCommentBan>(entity =>
        {
            entity.HasKey(e => e.BanId);
            entity.ToTable("UserCommentBan");
            entity.Property(e => e.BanId).ValueGeneratedOnAdd();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.BannedBy).HasMaxLength(20).HasDefaultValue("AI");
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CosmeticItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);
            entity.ToTable("CosmeticItem");
            entity.Property(e => e.ItemId).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UnlockType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AchievementKey).HasMaxLength(100);
            entity.Property(e => e.PreviewData).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(255);
        });

        modelBuilder.Entity<UserCosmetic>(entity =>
        {
            entity.HasKey(e => e.UserCosmeticId);
            entity.ToTable("UserCosmetic");
            entity.Property(e => e.UserCosmeticId).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.UserId, e.ItemId }).IsUnique();
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Item).WithMany(i => i.UserCosmetics).HasForeignKey(d => d.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserLoadout>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.ToTable("UserLoadout");
            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Frame).WithMany().HasForeignKey(d => d.FrameItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.NameColor).WithMany().HasForeignKey(d => d.NameColorItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.Banner).WithMany().HasForeignKey(d => d.BannerItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.Badge).WithMany().HasForeignKey(d => d.BadgeItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.Effect).WithMany().HasForeignKey(d => d.EffectItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(d => d.Card).WithMany().HasForeignKey(d => d.CardItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CommentReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);
            entity.ToTable("CommentReport");
            entity.Property(e => e.ReportId).ValueGeneratedOnAdd();
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.HasOne(e => e.Comment).WithMany().HasForeignKey(e => e.CommentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Reporter).WithMany().HasForeignKey(e => e.ReporterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.ToTable("SupportTicket");
            entity.Property(e => e.TicketId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("open");
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.LastAdminReplyAt).IsRequired(false);
            entity.Property(e => e.AutoCloseWarningSent).HasDefaultValue(false);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);
            entity.ToTable("SupportMessage");
            entity.Property(e => e.MessageId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.SenderRole).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.HasIndex(e => e.TicketId);
            entity.HasOne(e => e.Ticket).WithMany(t => t.Messages).HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
