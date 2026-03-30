using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Models.Subscriptions;

public class AdminSubscriptionPaymentUpdateResult
{
    public bool Success { get; set; }

    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public SubscriptionPayment? Payment { get; set; }

    public string[] Errors { get; set; } = [];
}
