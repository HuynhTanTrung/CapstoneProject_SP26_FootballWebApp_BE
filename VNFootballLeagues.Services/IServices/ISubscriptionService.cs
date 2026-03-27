using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Models.Subscriptions;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.IServices;

public interface ISubscriptionService
{
    IReadOnlyCollection<SubscriptionPlanSettings> GetAvailablePlans();
    Task<UserSubscription?> GetCurrentSubscriptionAsync(Guid userId);
    Task<SubscriptionPaymentCreateResult> CreatePaymentAsync(Guid userId, string planCode);
    Task<SubscriptionPayment?> GetPaymentByCodeAsync(Guid userId, string paymentCode);
}
