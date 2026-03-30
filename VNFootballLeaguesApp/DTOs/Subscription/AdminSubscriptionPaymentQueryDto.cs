namespace VNFootballLeaguesApp.DTOs.Subscription;

public class AdminSubscriptionPaymentQueryDto
{
    public string? Status { get; set; }

    public string? PaymentCode { get; set; }

    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
