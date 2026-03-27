namespace VNFootballLeagues.Services.Settings;

public class SubscriptionSettings
{
    public int PaymentExpiryMinutes { get; set; } = 30;

    public List<SubscriptionPlanSettings> Plans { get; set; } = [];
}

public class SubscriptionPlanSettings
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long Price { get; set; }

    public int DurationDays { get; set; }
}
