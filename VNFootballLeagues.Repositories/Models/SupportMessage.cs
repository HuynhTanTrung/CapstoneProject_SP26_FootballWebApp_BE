#nullable disable
using System;

namespace VNFootballLeagues.Repositories.Models;

/// <summary>Tin nhắn trong một ticket hỗ trợ.</summary>
public class SupportMessage
{
    public Guid MessageId { get; set; }
    public Guid TicketId { get; set; }

    /// <summary>user hoặc admin</summary>
    public string SenderRole { get; set; }

    public string Content { get; set; }

    /// <summary>URL ảnh trên Cloudinary (nếu có)</summary>
    public string ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SupportTicket Ticket { get; set; }
}
