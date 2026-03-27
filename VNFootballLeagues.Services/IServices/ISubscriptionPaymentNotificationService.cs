using VNFootballLeagues.Services.Models.Subscriptions;

namespace VNFootballLeagues.Services.IServices;

public interface ISubscriptionPaymentNotificationService
{
    SubscriptionPaymentNotificationSubscription Subscribe(string paymentCode);
    ValueTask PublishAsync(SubscriptionPaymentNotificationEvent notification);
}
