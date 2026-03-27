using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Models.Subscriptions;

public class SubscriptionPaymentCreateResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string[] Errors { get; set; } = [];

    public SubscriptionPayment? Payment { get; set; }
}
