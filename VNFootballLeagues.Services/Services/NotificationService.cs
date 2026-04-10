using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Services;

public class NotificationService
{
    private readonly VNFootballLeaguesDBContext _db;
    private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public NotificationService(VNFootballLeaguesDBContext db) => _db = db;

    // ── Create ────────────────────────────────────────────────────────────
    public async Task CreateAsync(Guid userId, string type, string title, string message, string? link = null, CancellationToken ct = default)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId, Type = type, Title = title,
            Message = message, Link = link, IsRead = false, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task CreateBulkAsync(IEnumerable<Guid> userIds, string type, string title, string message, string? link = null, CancellationToken ct = default)
    {
        foreach (var uid in userIds)
            _db.Notifications.Add(new Notification { UserId = uid, Type = type, Title = title, Message = message, Link = link, IsRead = false, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    // ── Query ─────────────────────────────────────────────────────────────
    public async Task<(List<NotificationDto> Items, int UnreadCount)> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);
        var total = await q.CountAsync(ct);
        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items.Select(Map).ToList(), unread);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    // ── Mark read ─────────────────────────────────────────────────────────
    public async Task MarkReadAsync(int notificationId, Guid userId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, ct);
        if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(ct); }
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private NotificationDto Map(Notification n) => new(
        n.NotificationId, n.Type, n.Title, n.Message, n.Link, n.IsRead,
        TimeZoneInfo.ConvertTimeFromUtc(n.CreatedAt, VnTz).ToString("HH:mm dd/MM/yyyy"));

    public record NotificationDto(int Id, string Type, string Title, string Message, string? Link, bool IsRead, string CreatedAt);

    // ── Predefined notification creators ─────────────────────────────────
    public Task WelcomeAsync(Guid userId, string username, CancellationToken ct = default) =>
        CreateAsync(userId, "welcome", "Chào mừng đến với VN Football! 🎉",
            $"Xin chào {username}! Khám phá thống kê cầu thủ, dự đoán trận đấu và tham gia diễn đàn bóng đá Việt Nam.", "/", ct);

    public Task SubscriptionSuccessAsync(Guid userId, string planName, DateTime expiresAt, CancellationToken ct = default) =>
        CreateAsync(userId, "subscription_success", "Đăng ký gói thành công! ✅",
            $"Gói {planName} đã được kích hoạt. Hạn sử dụng đến {TimeZoneInfo.ConvertTimeFromUtc(expiresAt, VnTz):HH:mm dd/MM/yyyy}.", "/pricing", ct);

    public Task TopUpSuccessAsync(Guid userId, string creditType, int amount, CancellationToken ct = default) =>
        CreateAsync(userId, "topup_success", "Nạp credit thành công! ✅",
            $"Đã nạp thêm {amount} {creditType}. Thời gian: {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz):HH:mm dd/MM/yyyy}.", "/pricing", ct);

    public Task SubscriptionExpiringAsync(Guid userId, string planName, DateTime expiresAt, CancellationToken ct = default) =>
        CreateAsync(userId, "subscription_expiring", "Gói sắp hết hạn ⚠️",
            $"Gói {planName} sẽ hết hạn lúc {TimeZoneInfo.ConvertTimeFromUtc(expiresAt, VnTz):HH:mm dd/MM/yyyy}. Gia hạn ngay để không bị gián đoạn.", "/pricing", ct);

    public Task CommentReplyAsync(Guid userId, string replierName, string postTitle, int postId, CancellationToken ct = default) =>
        CreateAsync(userId, "comment_reply", $"{replierName} đã trả lời bình luận của bạn 💬",
            $"Trong bài: \"{postTitle}\"", $"/forum/{postId}", ct);

    public Task CommentWarningAsync(Guid userId, string reason, CancellationToken ct = default) =>
        CreateAsync(userId, "comment_warning", "Cảnh báo vi phạm ngôn từ ⚠️",
            $"Bình luận của bạn đã bị ẩn: {reason}. Tiếp tục vi phạm sẽ bị cấm bình luận.", null, ct);

    public Task ReportApprovedForReporterAsync(Guid reporterId, string postTitle, int postId, CancellationToken ct = default) =>
        CreateAsync(reporterId, "report_approved", "Báo cáo được chấp nhận ✅",
            $"Báo cáo của bạn về bình luận trong bài \"{postTitle}\" đã được xử lý. Bình luận vi phạm đã bị ẩn.", $"/forum/{postId}", ct);

    public Task ReportApprovedForAuthorAsync(Guid authorId, string postTitle, int postId, CancellationToken ct = default) =>
        CreateAsync(authorId, "comment_hidden_report", "Bình luận bị ẩn do báo cáo 🚫",
            $"Một bình luận của bạn trong bài \"{postTitle}\" đã bị ẩn sau khi được báo cáo và xem xét bởi Admin.", $"/forum/{postId}", ct);

    public Task ReportDismissedAsync(Guid reporterId, string reason, CancellationToken ct = default) =>
        CreateAsync(reporterId, "report_dismissed", "Báo cáo không được chấp nhận ℹ️",
            $"Báo cáo của bạn đã được xem xét nhưng không đủ cơ sở để xử lý. {(string.IsNullOrEmpty(reason) ? "" : $"Lý do: {reason}")}", null, ct);

    public Task CommentBanAsync(Guid userId, int days, string reason, string bannedBy, CancellationToken ct = default) =>
        CreateAsync(userId, "comment_ban", $"Bị cấm bình luận {days} ngày 🚫",
            $"Lý do: {reason}. Cấm bởi: {bannedBy}.", null, ct);

    public Task CommentUnbannedAsync(Guid userId, CancellationToken ct = default) =>
        CreateAsync(userId, "comment_unbanned", "Lệnh cấm bình luận đã được gỡ ✅",
            $"Tài khoản của bạn đã được gỡ cấm bình luận lúc {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz):HH:mm dd/MM/yyyy}. Hãy tuân thủ quy định cộng đồng.", null, ct);

    public Task CosmeticPurchaseAsync(Guid userId, string itemName, int pointsSpent, int remaining, CancellationToken ct = default) =>
        CreateAsync(userId, "cosmetic_purchase", "Đổi quà thành công! 🎁",
            $"Bạn đã dùng {pointsSpent}đ để mua \"{itemName}\". Còn lại: {remaining}đ.", "/shop", ct);

    public Task AchievementUnlockedAsync(Guid userId, string achievementName, string description, CancellationToken ct = default) =>
        CreateAsync(userId, "achievement_unlocked", $"Mở khóa thành tích: {achievementName} 🏆",
            description, "/profile", ct);

    public Task PostApprovedAsync(Guid userId, string postTitle, int postId, CancellationToken ct = default) =>
        CreateAsync(userId, "post_approved", "Bài đăng đã được duyệt ✅",
            $"\"{postTitle}\" đã được duyệt và hiển thị công khai.", $"/forum/{postId}", ct);

    public Task PostRejectedAsync(Guid userId, string postTitle, string reason, CancellationToken ct = default) =>
        CreateAsync(userId, "post_rejected", "Bài đăng bị từ chối ❌",
            $"\"{postTitle}\" bị từ chối. Lý do: {reason}.", "/forum", ct);

    public Task PostHiddenAsync(Guid userId, string postTitle, CancellationToken ct = default) =>
        CreateAsync(userId, "post_hidden", "Bài đăng bị ẩn 🔒",
            $"\"{postTitle}\" đã bị admin ẩn do vi phạm quy định cộng đồng.", "/forum", ct);

    public Task PredictionResultAsync(Guid userId, string matchName, bool correct, bool exactScore, int points, CancellationToken ct = default) =>
        CreateAsync(userId, "prediction_result", correct ? "Dự đoán chính xác! 🎯" : "Kết quả dự đoán",
            correct ? $"{matchName}: {(exactScore ? "Đúng tỉ số" : "Đúng kết quả")} +{points}đ" : $"{matchName}: Dự đoán chưa chính xác lần này.",
            "/predictions", ct);

    public Task ContestResultAsync(Guid userId, string contestTitle, int points, CancellationToken ct = default) =>
        CreateAsync(userId, "contest_result", "Kết quả dự đoán đặc biệt 🏅",
            points > 0 ? $"{contestTitle}: +{points}đ" : $"{contestTitle}: Chưa chính xác.", "/predictions", ct);

    public Task CheckinStreakAsync(Guid userId, int streak, CancellationToken ct = default) =>
        CreateAsync(userId, "checkin_streak", $"Chuỗi điểm danh {streak} ngày! 🔥",
            streak >= 100 ? "Xuất sắc! Bạn đã đạt chuỗi 100 ngày và nhận khung kim cương 💎" :
            streak >= 30 ? "Tuyệt vời! Bạn đã đạt chuỗi 30 ngày và nhận khung lửa đỏ 🔥" :
            "Bạn đã đạt chuỗi 7 ngày và nhận khung đồng 🥉", "/profile", ct);

    public Task PointsMilestoneAsync(Guid userId, int points, CancellationToken ct = default) =>
        CreateAsync(userId, "points_milestone", $"Đạt mốc {points} điểm! ⭐",
            $"Chúc mừng! Bạn đã tích lũy được {points} điểm. Vào shop để đổi quà.", "/shop", ct);

    public Task AdminWarningAsync(Guid userId, string message, CancellationToken ct = default) =>
        CreateAsync(userId, "admin_warning", "Thông báo từ Admin 📢", message, null, ct);

    public Task NewFeatureAsync(Guid userId, string featureName, string description, CancellationToken ct = default) =>
        CreateAsync(userId, "new_feature", $"Tính năng mới: {featureName} 🆕", description, null, ct);

    public Task PasswordChangedAsync(Guid userId, CancellationToken ct = default) =>
        CreateAsync(userId, "password_changed", "Mật khẩu đã được thay đổi 🔐",
            "Mật khẩu tài khoản của bạn vừa được cập nhật. Nếu không phải bạn, hãy liên hệ hỗ trợ ngay.", null, ct);

    public Task EmailVerifiedAsync(Guid userId, CancellationToken ct = default) =>
        CreateAsync(userId, "email_verified", "Email đã được xác thực ✅",
            "Tài khoản của bạn đã được xác thực thành công. Chào mừng bạn!", "/", ct);

    public Task PostPopularAsync(Guid userId, string postTitle, int postId, int views, CancellationToken ct = default) =>
        CreateAsync(userId, "post_popular", "Bài đăng của bạn đang hot! 🔥",
            $"\"{postTitle}\" đã đạt {views} lượt xem.", $"/forum/{postId}", ct);
}
