using System.Threading.Channels;

namespace VNFootballLeagues.Services.Models.Subscriptions;

public sealed class SubscriptionPaymentNotificationSubscription : IAsyncDisposable
{
    private readonly Func<ValueTask> _unsubscribeAsync;
    private bool _disposed;

    public SubscriptionPaymentNotificationSubscription(
        string paymentCode,
        ChannelReader<SubscriptionPaymentNotificationEvent> reader,
        Func<ValueTask> unsubscribeAsync)
    {
        PaymentCode = paymentCode;
        Reader = reader;
        _unsubscribeAsync = unsubscribeAsync;
    }

    public string PaymentCode { get; }

    public ChannelReader<SubscriptionPaymentNotificationEvent> Reader { get; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _unsubscribeAsync();
    }
}
