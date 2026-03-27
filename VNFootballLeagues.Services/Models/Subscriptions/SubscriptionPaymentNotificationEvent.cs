using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Models.Subscriptions;

public class SubscriptionPaymentNotificationEvent
{
    public string EventName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public SubscriptionPayment Payment { get; set; } = null!;
}
