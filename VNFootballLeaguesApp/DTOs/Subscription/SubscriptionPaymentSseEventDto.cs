namespace VNFootballLeaguesApp.DTOs.Subscription;

public class SubscriptionPaymentSseEventDto
{
    public string Event { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public SubscriptionPaymentDto Payment { get; set; } = new();
}
