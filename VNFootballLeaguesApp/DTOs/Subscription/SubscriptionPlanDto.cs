namespace VNFootballLeaguesApp.DTOs.Subscription;

public class SubscriptionPlanDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long Price { get; set; }

    public int DurationDays { get; set; }
}
