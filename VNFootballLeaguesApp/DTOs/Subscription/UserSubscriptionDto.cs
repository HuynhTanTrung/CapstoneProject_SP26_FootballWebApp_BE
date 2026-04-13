namespace VNFootballLeaguesApp.DTOs.Subscription;

public class UserSubscriptionDto
{
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastPaymentAt { get; set; }
    public int AiVideoCreditsRemaining { get; set; }
    public int ForumPostCreditsRemaining { get; set; }
    public int AiMatchAnalysisRemaining { get; set; }
}
