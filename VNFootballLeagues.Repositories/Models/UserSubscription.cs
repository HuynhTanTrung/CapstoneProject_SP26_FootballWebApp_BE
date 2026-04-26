using System;

namespace VNFootballLeagues.Repositories.Models;

public partial class UserSubscription
{
    public Guid UserId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastPaymentAt { get; set; }

    public int AiVideoCreditsRemaining { get; set; } = 0;

    public int ForumPostCreditsRemaining { get; set; } = 0;

    public int AiMatchAnalysisRemaining { get; set; } = 0;

    public int AiArticleCreditsRemaining { get; set; } = 0;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
