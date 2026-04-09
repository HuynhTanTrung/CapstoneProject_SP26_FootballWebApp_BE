namespace VNFootballLeagues.Repositories.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>welcome|subscription_success|subscription_expiring|comment_reply|comment_warning|
    /// comment_ban|cosmetic_purchase|achievement_unlocked|post_approved|post_rejected|post_hidden|
    /// prediction_result|contest_result|checkin_streak|points_milestone|new_feature|
    /// password_changed|email_verified|admin_warning|comment_liked|post_popular</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}
