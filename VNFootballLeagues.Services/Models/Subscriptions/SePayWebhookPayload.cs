namespace VNFootballLeagues.Services.Models.Subscriptions;

public class SePayWebhookPayload
{
    public long Id { get; set; }

    public string Gateway { get; set; } = string.Empty;

    public string TransactionDate { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Content { get; set; }

    public string TransferType { get; set; } = string.Empty;

    public long TransferAmount { get; set; }

    public long Accumulated { get; set; }

    public string? SubAccount { get; set; }

    public string? ReferenceCode { get; set; }

    public string? Description { get; set; }
}
