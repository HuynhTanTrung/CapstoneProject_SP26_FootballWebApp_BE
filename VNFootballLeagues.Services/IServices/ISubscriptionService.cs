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
    Task<AdminSubscriptionPaymentListResult> GetPaymentsForAdminAsync(
        string? status,
        string? paymentCode,
        string? keyword,
        int pageNumber,
        int pageSize);
    Task<SubscriptionPayment?> GetPaymentByCodeForAdminAsync(string paymentCode);
    Task<AdminSubscriptionPaymentUpdateResult> ManuallyUpdatePaymentStatusAsync(
        Guid adminUserId,
        string paymentCode,
        string status,
        string? reason,
        DateTime? paidAt,
        string? referenceCode,
        string? gateway);
}
