using System.Collections.Concurrent;
using System.Threading.Channels;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;

namespace VNFootballLeagues.Services.Services;

public class SubscriptionPaymentNotificationService : ISubscriptionPaymentNotificationService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<SubscriptionPaymentNotificationEvent>>> _subscriptions =
        new(StringComparer.OrdinalIgnoreCase);

    public SubscriptionPaymentNotificationSubscription Subscribe(string paymentCode)
    {
        var normalizedPaymentCode = NormalizePaymentCode(paymentCode);
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<SubscriptionPaymentNotificationEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var subscribers = _subscriptions.GetOrAdd(
            normalizedPaymentCode,
            _ => new ConcurrentDictionary<Guid, Channel<SubscriptionPaymentNotificationEvent>>());

        subscribers[subscriptionId] = channel;

        return new SubscriptionPaymentNotificationSubscription(
            normalizedPaymentCode,
            channel.Reader,
            () => UnsubscribeAsync(normalizedPaymentCode, subscriptionId));
    }

    public ValueTask PublishAsync(SubscriptionPaymentNotificationEvent notification)
    {
        if (notification.Payment is null || string.IsNullOrWhiteSpace(notification.Payment.PaymentCode))
        {
            return ValueTask.CompletedTask;
        }

        var normalizedPaymentCode = NormalizePaymentCode(notification.Payment.PaymentCode);
        if (!_subscriptions.TryGetValue(normalizedPaymentCode, out var subscribers))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var subscriber in subscribers.ToArray())
        {
            if (subscriber.Value.Writer.TryWrite(notification))
            {
                continue;
            }

            if (subscribers.TryRemove(subscriber.Key, out var removedChannel))
            {
                removedChannel.Writer.TryComplete();
            }
        }

        if (subscribers.IsEmpty)
        {
            _subscriptions.TryRemove(normalizedPaymentCode, out _);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask UnsubscribeAsync(string paymentCode, Guid subscriptionId)
    {
        if (_subscriptions.TryGetValue(paymentCode, out var subscribers) &&
            subscribers.TryRemove(subscriptionId, out var channel))
        {
            channel.Writer.TryComplete();

            if (subscribers.IsEmpty)
            {
                _subscriptions.TryRemove(paymentCode, out _);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static string NormalizePaymentCode(string paymentCode)
    {
        return paymentCode.Trim().ToUpperInvariant();
    }
}
