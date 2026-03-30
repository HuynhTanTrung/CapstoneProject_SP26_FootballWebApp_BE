namespace VNFootballLeaguesApp.DTOs.Subscription;

public class AdminSubscriptionPaymentDto : SubscriptionPaymentDto
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public Guid? ManualUpdatedByUserId { get; set; }

    public string? ManualUpdatedByName { get; set; }

    public DateTime? ManualUpdatedAt { get; set; }

    public string? ManualUpdateReason { get; set; }
}
