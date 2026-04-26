using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly VNFootballLeaguesDBContext _db;
    private readonly IUserService _userService;
    private readonly CloudinaryService _cloudinary;

    public SupportController(VNFootballLeaguesDBContext db, IUserService userService, CloudinaryService cloudinary)
    {
        _db = db;
        _userService = userService;
        _cloudinary = cloudinary;
    }

    // ─── USER ENDPOINTS ───────────────────────────────────────────────────────

    /// <summary>Chỉ lấy ticket hiện tại (không tạo mới). Trả về 404 nếu chưa có.</summary>
    [HttpGet("my-ticket/current")]
    [Authorize]
    public async Task<IActionResult> GetCurrentTicket()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        // Lấy ticket có tin nhắn gần nhất (bỏ qua ticket rỗng)
        var ticket = await _db.SupportTickets
            .Where(t => t.UserId == userId.Value && _db.SupportMessages.Any(m => m.TicketId == t.TicketId))
            .OrderByDescending(t => t.Status == "open" || t.Status == "in_progress" ? 1 : 0)
            .ThenByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync();

        if (ticket == null) return NotFound();
        return Ok(MapTicket(ticket));
    }

    /// <summary>Lấy ticket hiện tại của user (tạo mới nếu chưa có ticket open/in_progress).</summary>
    [HttpGet("my-ticket")]
    [Authorize]
    public async Task<IActionResult> GetOrCreateMyTicket([FromQuery] string? category, [FromQuery] string? subject)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var ticket = await _db.SupportTickets
            .Include(t => t.Messages)
            .Where(t => t.UserId == userId.Value && (t.Status == "open" || t.Status == "in_progress"))
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync();

        if (ticket == null)
        {
            ticket = new SupportTicket
            {
                TicketId = Guid.NewGuid(),
                UserId = userId.Value,
                Category = category ?? "other",
                Subject = subject ?? "Yêu cầu hỗ trợ",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UnreadByAdmin = 0
            };
            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();
        }

        return Ok(MapTicket(ticket));
    }

    /// <summary>Gửi tin nhắn (user).</summary>
    [HttpPost("my-ticket/messages")]
    [Authorize]
    public async Task<IActionResult> SendUserMessage([FromForm] SendMessageRequest req)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.TicketId == req.TicketId && t.UserId == userId.Value);
        if (ticket == null) return NotFound();

        string? imageUrl = null;
        if (req.Image != null && req.Image.Length > 0)
        {
            using var stream = req.Image.OpenReadStream();
            imageUrl = await _cloudinary.UploadSupportImageAsync(stream, req.Image.FileName, ticket.TicketId.ToString());
        }

        var msg = new SupportMessage
        {
            MessageId = Guid.NewGuid(),
            TicketId = ticket.TicketId,
            SenderRole = "user",
            Content = req.Content ?? "",
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportMessages.Add(msg);

        ticket.UnreadByAdmin += 1;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status == "resolved" || ticket.Status == "closed")
            ticket.Status = "open";

        await _db.SaveChangesAsync();
        return Ok(MapMessage(msg));
    }

    /// <summary>Lấy tin nhắn của ticket.</summary>
    [HttpGet("my-ticket/{ticketId}/messages")]
    [Authorize]
    public async Task<IActionResult> GetMyMessages(Guid ticketId)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId && t.UserId == userId.Value);
        if (ticket == null) return NotFound();

        var messages = await _db.SupportMessages
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(messages.Select(MapMessage));
    }

    // ─── ADMIN ENDPOINTS ──────────────────────────────────────────────────────

    /// <summary>Lấy tất cả tickets (admin). Trả về số unread tổng để hiện badge.</summary>
    [HttpGet("admin/tickets")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminGetTickets([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.Messages)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var total = await query.CountAsync();
        var totalUnread = await _db.SupportTickets.SumAsync(t => t.UnreadByAdmin);

        var tickets = await query
            .OrderByDescending(t => t.UnreadByAdmin)
            .ThenByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            total,
            totalUnread,
            page,
            pageSize,
            items = tickets.Select(t => new
            {
                ticketId = t.TicketId,
                userId = t.UserId,
                username = t.User?.Username,
                fullName = t.User?.FullName,
                avatarUrl = t.User?.AvatarUrl,
                category = t.Category,
                subject = t.Subject,
                status = t.Status,
                unreadByAdmin = t.UnreadByAdmin,
                createdAt = t.CreatedAt,
                updatedAt = t.UpdatedAt,
                lastMessage = t.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content
            })
        });
    }

    /// <summary>Lấy tin nhắn của 1 ticket (admin).</summary>
    [HttpGet("admin/tickets/{ticketId}/messages")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminGetMessages(Guid ticketId)
    {
        var ticket = await _db.SupportTickets.Include(t => t.User).FirstOrDefaultAsync(t => t.TicketId == ticketId);
        if (ticket == null) return NotFound();

        // Mark as read
        if (ticket.UnreadByAdmin > 0)
        {
            ticket.UnreadByAdmin = 0;
            await _db.SaveChangesAsync();
        }

        var messages = await _db.SupportMessages
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            ticket = new
            {
                ticketId = ticket.TicketId,
                userId = ticket.UserId,
                username = ticket.User?.Username,
                fullName = ticket.User?.FullName,
                avatarUrl = ticket.User?.AvatarUrl,
                category = ticket.Category,
                subject = ticket.Subject,
                status = ticket.Status
            },
            messages = messages.Select(MapMessage)
        });
    }

    /// <summary>Admin gửi tin nhắn trả lời.</summary>
    [HttpPost("admin/tickets/{ticketId}/messages")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminSendMessage(Guid ticketId, [FromForm] AdminSendMessageRequest req)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
        if (ticket == null) return NotFound();

        string? imageUrl = null;
        if (req.Image != null && req.Image.Length > 0)
        {
            using var stream = req.Image.OpenReadStream();
            imageUrl = await _cloudinary.UploadSupportImageAsync(stream, req.Image.FileName, ticketId.ToString());
        }

        var msg = new SupportMessage
        {
            MessageId = Guid.NewGuid(),
            TicketId = ticketId,
            SenderRole = "admin",
            Content = req.Content ?? "",
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportMessages.Add(msg);

        ticket.Status = "in_progress";
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.LastAdminReplyAt = DateTime.UtcNow;
        ticket.AutoCloseWarningSent = false; // reset khi admin reply mới
        await _db.SaveChangesAsync();

        return Ok(MapMessage(msg));
    }

    /// <summary>Admin cập nhật trạng thái ticket.</summary>
    [HttpPatch("admin/tickets/{ticketId}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminUpdateStatus(Guid ticketId, [FromBody] UpdateStatusRequest req)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
        if (ticket == null) return NotFound();

        var oldStatus = ticket.Status;
        ticket.Status = req.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Gửi tin nhắn hệ thống thông báo cho user
        string? systemMsg = (req.Status, oldStatus) switch
        {
            ("resolved", _) => "Yêu cầu hỗ trợ của bạn đã được đóng bởi Admin. Nếu cần hỗ trợ thêm, hãy nhắn tin mới.",
            ("open", "resolved") or ("open", "closed") or ("in_progress", "resolved") => "Admin đã mở lại yêu cầu hỗ trợ của bạn. Chúng tôi sẽ tiếp tục hỗ trợ bạn.",
            _ => null
        };

        if (systemMsg != null)
        {
            _db.SupportMessages.Add(new SupportMessage
            {
                MessageId = Guid.NewGuid(),
                TicketId = ticketId,
                SenderRole = "admin",
                Content = systemMsg,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>Admin cập nhật gói thủ công cho user.</summary>
    [HttpPost("admin/manual-grant-subscription")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ManualGrantSubscription([FromBody] ManualGrantRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == req.UserId);
        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var planMap = new Dictionary<string, (string name, int days, int aiVideo, int forumPost, int aiArticle)>
        {
            ["TRIAL"]              = ("Gói Dùng thử (3 ngày)",                  3,  1,  2,  10),
            ["MONTHLY"]            = ("Gói tháng (30 ngày)",                    30, 15, 15, 30),
            ["QUARTERLY"]          = ("Gói quý (90 ngày)",                      90, 45, 50, 100),
            ["TOPUP_AI_VIDEO"]     = ("Nạp thêm AI Video Analysis (5 lượt)",    0,  5,  0,  0),
            ["TOPUP_FORUM_POST"]   = ("Nạp thêm bài đăng diễn đàn (10 bài)",   0,  0,  10, 0),
            ["TOPUP_AI_ARTICLE"]   = ("Nạp thêm AI Phân tích bài viết (10 lượt)", 0, 0, 0, 10),
        };

        if (!planMap.TryGetValue(req.PlanCode, out var plan))
            return BadRequest(new { message = "Mã gói không hợp lệ." });

        var now = DateTime.UtcNow;
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == req.UserId);

        if (req.PlanCode.StartsWith("TOPUP_"))
        {
            // Nạp credit — cộng dồn
            if (sub == null)
                return BadRequest(new { message = "User chưa có gói nào để nạp credit." });
            sub.AiVideoCreditsRemaining += plan.aiVideo;
            sub.ForumPostCreditsRemaining += plan.forumPost;
            sub.AiArticleCreditsRemaining += plan.aiArticle;
            sub.UpdatedAt = now;
        }
        else
        {
            // Tính ngày cộng dồn: nếu còn hạn thì cộng từ ExpiresAt, không thì tính từ now
            var baseDate = sub != null && sub.ExpiresAt > now ? sub.ExpiresAt : now;

            if (sub == null)
            {
                sub = new UserSubscription
                {
                    UserId = req.UserId,
                    PlanCode = req.PlanCode,
                    PlanName = plan.name,
                    Status = "Active",
                    StartedAt = now,
                    ExpiresAt = baseDate.AddDays(plan.days),
                    LastPaymentAt = now,
                    AiVideoCreditsRemaining = plan.aiVideo,
                    ForumPostCreditsRemaining = plan.forumPost,
                    AiArticleCreditsRemaining = plan.aiArticle,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.UserSubscriptions.Add(sub);
            }
            else
            {
                if (sub.ExpiresAt <= now) sub.StartedAt = now;

                // Chỉ ghi đè PlanCode/PlanName nếu gói mới cao hơn hoặc bằng gói hiện tại
                var planOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TRIAL"] = 1, ["MONTHLY"] = 2, ["QUARTERLY"] = 3
                };
                var currentOrder = planOrder.TryGetValue(sub.PlanCode ?? "", out var co) ? co : 0;
                var newOrder = planOrder.TryGetValue(req.PlanCode, out var no) ? no : 0;
                if (newOrder >= currentOrder)
                {
                    sub.PlanCode = req.PlanCode;
                    sub.PlanName = plan.name;
                }

                sub.Status = "Active";
                sub.ExpiresAt = baseDate.AddDays(plan.days);
                sub.LastPaymentAt = now;
                sub.AiVideoCreditsRemaining += plan.aiVideo;
                sub.ForumPostCreditsRemaining += plan.forumPost;
                sub.AiArticleCreditsRemaining += plan.aiArticle;
                sub.UpdatedAt = now;
            }
        }

        // Ghi chú vào ticket nếu có
        if (req.TicketId.HasValue)
        {
            var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == req.TicketId.Value);
            if (ticket != null)
            {
                _db.SupportMessages.Add(new SupportMessage
                {
                    MessageId = Guid.NewGuid(),
                    TicketId = ticket.TicketId,
                    SenderRole = "admin",
                    Content = $"Admin đã cập nhật gói **{plan.name}** cho tài khoản của bạn thủ công. Vui lòng đăng xuất và đăng nhập lại để thấy thay đổi.",
                    CreatedAt = now
                });
                ticket.Status = "resolved";
                ticket.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = $"Đã cấp gói {plan.name} cho user {req.UserId}." });
    }

    /// <summary>Tổng số unread (dùng cho badge navbar).</summary>
    [HttpGet("admin/unread-count")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _db.SupportTickets.SumAsync(t => t.UnreadByAdmin);
        return Ok(new { count });
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────────

    private static object MapTicket(SupportTicket t) => new
    {
        ticketId = t.TicketId,
        category = t.Category,
        subject = t.Subject,
        status = t.Status,
        createdAt = t.CreatedAt,
        updatedAt = t.UpdatedAt
    };

    private static object MapMessage(SupportMessage m) => new
    {
        messageId = m.MessageId,
        ticketId = m.TicketId,
        senderRole = m.SenderRole,
        content = m.Content,
        imageUrl = m.ImageUrl,
        createdAt = m.CreatedAt
    };
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public class SendMessageRequest
{
    public Guid TicketId { get; set; }
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
}

public class AdminSendMessageRequest
{
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = "resolved";
}

public class ManualGrantRequest
{
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = "";
    public Guid? TicketId { get; set; }
}
