using System;

namespace VNFootballLeagues.Repositories.Models;

public partial class SePayWebhookLog
{
    public Guid WebhookLogId { get; set; }

    public long SePayTransactionId { get; set; }

    public string? PaymentCode { get; set; }

    public string? ReferenceCode { get; set; }

    public string TransferType { get; set; } = string.Empty;

    public long TransferAmount { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public string ProcessingStatus { get; set; } = string.Empty;

    public string? ProcessingMessage { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
