#nullable disable
using System;
using System.Collections.Generic;

namespace VNFootballLeagues.Repositories.Models;

/// <summary>Ticket hỗ trợ người dùng gửi cho admin (chủ yếu về thanh toán/gói).</summary>
public class SupportTicket
{
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Loại vấn đề: wrong_transfer_content, payment_not_updated, expired_not_renewed,
    /// credit_not_added, reward_deducted, refund_request, other</summary>
    public string Category { get; set; } = "other";

    /// <summary>Trạng thái: open, in_progress, resolved, closed</summary>
    public string Status { get; set; } = "open";

    public string Subject { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Số tin nhắn chưa đọc phía admin (để hiện badge)</summary>
    public int UnreadByAdmin { get; set; } = 0;

    /// <summary>Thời điểm admin reply cuối cùng (dùng để tính auto-close)</summary>
    public DateTime? LastAdminReplyAt { get; set; }

    /// <summary>Đã gửi cảnh báo auto-close chưa</summary>
    public bool AutoCloseWarningSent { get; set; } = false;

    public virtual User User { get; set; }
    public virtual ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
