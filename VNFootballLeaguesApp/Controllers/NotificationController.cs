using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _svc;
    private readonly IUserService _userService;

    public NotificationController(NotificationService svc, IUserService userService)
    {
        _svc = svc;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (items, unread) = await _svc.GetForUserAsync(userId.Value, page, pageSize, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = new { items, unread, page, pageSize } });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var count = await _svc.GetUnreadCountAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = new { count } });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        await _svc.MarkReadAsync(id, userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        await _svc.MarkAllReadAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã đánh dấu tất cả là đã đọc." });
    }

    // Admin: send broadcast notification
    [HttpPost("admin/broadcast")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<VNFootballLeagues.Repositories.Models.VNFootballLeaguesDBContext>();
        var ids = await db.Users.Select(u => u.UserId).ToListAsync(ct);
        await _svc.CreateBulkAsync(ids, "new_feature", req.Title, req.Message, req.Link, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = $"Đã gửi thông báo đến {ids.Count} người dùng." });
    }
}

public record BroadcastRequest(string Title, string Message, string? Link);
