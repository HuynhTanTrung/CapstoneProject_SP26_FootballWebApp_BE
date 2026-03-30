namespace VNFootballLeaguesApp.DTOs.Subscription;

public class AdminManualUpdateSubscriptionPaymentRequestDto
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? ReferenceCode { get; set; }

    public string? Gateway { get; set; }
}
