using System;

namespace VNFootballLeagues.Repositories.Models;

public partial class SubscriptionPayment
{
    public Guid PaymentId { get; set; }

    public Guid UserId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public long Amount { get; set; }

    public int DurationDays { get; set; }

    public string PaymentCode { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string BankCode { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string TransferContent { get; set; } = string.Empty;

    public string QrUrl { get; set; } = string.Empty;

    public long? SePayTransactionId { get; set; }

    public string? SePayReferenceCode { get; set; }

    public string? Gateway { get; set; }

    public DateTime? SePayTransactionDate { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
