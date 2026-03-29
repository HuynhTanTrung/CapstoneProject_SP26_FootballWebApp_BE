using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Models.Subscriptions;

public class AdminSubscriptionPaymentListResult
{
    public IReadOnlyCollection<SubscriptionPayment> Payments { get; set; } = [];

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
