using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeaguesApp.Jobs;

/// <summary>
/// Tự động đóng ticket hỗ trợ sau khi admin đã reply mà user không phản hồi.
/// Chạy mỗi 1 phút.
/// Flow:
///   - Admin reply → LastAdminReplyAt được set, AutoCloseWarningSent = false
///   - Sau 30 phút không có tin nhắn mới từ user → gửi cảnh báo "sẽ tự đóng sau 2 phút"
///   - Sau thêm 2 phút → đóng ticket
/// </summary>
public class SupportAutoCloseJob
{
    private readonly VNFootballLeaguesDBContext _db;
    private readonly ILogger<SupportAutoCloseJob> _logger;

    // Thời gian chờ sau khi admin reply trước khi gửi cảnh báo
    private static readonly TimeSpan WarnAfter = TimeSpan.FromMinutes(30);
    // Thời gian chờ sau khi gửi cảnh báo trước khi đóng
    private static readonly TimeSpan CloseAfterWarn = TimeSpan.FromSeconds(120);

    public SupportAutoCloseJob(VNFootballLeaguesDBContext db, ILogger<SupportAutoCloseJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;

        // Lấy tất cả ticket đang in_progress có LastAdminReplyAt
        var tickets = await _db.SupportTickets
            .Where(t => t.Status == "in_progress" && t.LastAdminReplyAt != null)
            .ToListAsync();

        foreach (var ticket in tickets)
        {
            var lastAdminReply = ticket.LastAdminReplyAt!.Value;

            // Kiểm tra xem user có nhắn gì sau lần admin reply cuối không
            var hasUserReplyAfter = await _db.SupportMessages
                .AnyAsync(m => m.TicketId == ticket.TicketId
                            && m.SenderRole == "user"
                            && m.CreatedAt > lastAdminReply);

            if (hasUserReplyAfter)
            {
                // User đã reply → reset, không auto-close
                ticket.AutoCloseWarningSent = false;
                continue;
            }

            var timeSinceAdminReply = now - lastAdminReply;

            if (!ticket.AutoCloseWarningSent && timeSinceAdminReply >= WarnAfter)
            {
                // Gửi tin nhắn cảnh báo
                _db.SupportMessages.Add(new SupportMessage
                {
                    MessageId = Guid.NewGuid(),
                    TicketId = ticket.TicketId,
                    SenderRole = "admin",
                    Content = "Yêu cầu hỗ trợ của bạn sẽ tự động đóng sau 2 phút nếu không có thêm thắc mắc. Nếu bạn cần hỗ trợ thêm, hãy nhắn tin ngay.",
                    CreatedAt = now
                });
                ticket.AutoCloseWarningSent = true;
                ticket.UpdatedAt = now;
                _logger.LogInformation("Sent auto-close warning for ticket {TicketId}", ticket.TicketId);
            }
            else if (ticket.AutoCloseWarningSent && timeSinceAdminReply >= WarnAfter + CloseAfterWarn)
            {
                // Đóng ticket
                _db.SupportMessages.Add(new SupportMessage
                {
                    MessageId = Guid.NewGuid(),
                    TicketId = ticket.TicketId,
                    SenderRole = "admin",
                    Content = "Yêu cầu hỗ trợ đã được tự động đóng. Nếu bạn cần hỗ trợ thêm, hãy mở chat và nhắn tin mới.",
                    CreatedAt = now
                });
                ticket.Status = "resolved";
                ticket.UpdatedAt = now;
                _logger.LogInformation("Auto-closed ticket {TicketId}", ticket.TicketId);
            }
        }

        await _db.SaveChangesAsync();
    }
}
