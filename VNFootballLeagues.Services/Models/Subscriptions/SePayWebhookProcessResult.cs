namespace VNFootballLeagues.Services.Models.Subscriptions;

public class SePayWebhookProcessResult
{
    public bool Success { get; set; }

    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;
}
