namespace VNFootballLeagues.Services.Models.Subscriptions;

public static class SubscriptionStatuses
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Inactive = "Inactive";
}

public static class SubscriptionPaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
}

public static class SePayWebhookProcessingStatuses
{
    public const string Received = "Received";
    public const string Processed = "Processed";
    public const string Ignored = "Ignored";
}
