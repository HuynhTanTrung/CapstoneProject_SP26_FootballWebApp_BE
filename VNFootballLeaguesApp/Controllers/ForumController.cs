using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/forum")]
public class ForumController : ControllerBase
{
    private readonly ForumService _forum;
    private readonly IUserService _userService;

    public ForumController(ForumService forum, IUserService userService)
    {
        _forum = forum;
        _userService = userService;
    }

    // ── Public ────────────────────────────────────────────────────────────

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts([FromQuery] string? leagueTag, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _forum.GetPublicPostsAsync(leagueTag, page, pageSize, ct);
        return Ok(new { success = true, data = items, total, page, pageSize });
    }

    [HttpGet("posts/{id}")]
    public async Task<IActionResult> GetPost(int id, CancellationToken ct)
    {
        var post = await _forum.GetPostAsync(id, ct);
        if (post == null) return NotFound();
        return Ok(new { success = true, data = post });
    }

    [HttpGet("posts/{id}/comments")]
    public async Task<IActionResult> GetComments(int id, CancellationToken ct)
    {
        var comments = await _forum.GetCommentsAsync(id, ct);
        return Ok(new { success = true, data = comments });
    }

    // ── Authenticated ─────────────────────────────────────────────────────

    [HttpGet("posts/my")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetMyPosts(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var posts = await _forum.GetMyPostsAsync(userId.Value, ct);
        return Ok(new { success = true, data = posts });
    }

    [HttpPost("posts")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var (success, message, postId) = await _forum.CreatePostAsync(
            userId.Value, req.Title, req.Content, req.LeagueTag,
            req.MediaUrls ?? new(), req.MediaTypes ?? new(), ct);

        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message, Data = new { postId } });
    }

    [HttpPost("posts/{id}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _forum.AddCommentAsync(id, userId.Value, req.Content, req.ParentCommentId, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpPut("posts/{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> EditPost(int id, [FromBody] EditPostRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _forum.EditPostAsync(id, userId.Value, req.Title, req.Content, req.MediaUrls, req.MediaTypes, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpDelete("posts/{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> DeletePost(int id, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _forum.DeletePostAsync(id, userId.Value, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpPost("posts/{id}/reactions")]
    [Authorize]
    public async Task<IActionResult> ToggleReaction(int id, [FromBody] ReactionRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, myReaction, counts) = await _forum.ToggleReactionAsync(id, userId.Value, req.ReactionType, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = new { myReaction, counts } });
    }

    [HttpGet("posts/{id}/reactions")]
    public async Task<IActionResult> GetReactions(int id, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        var (counts, myReaction) = await _forum.GetReactionsAsync(id, userId, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = new { counts, myReaction } });
    }

    // ── Admin ─────────────────────────────────────────────────────────────

    [HttpGet("admin/posts")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminGetPosts([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _forum.GetAdminPostsAsync(status, page, pageSize, ct);
        return Ok(new { success = true, data = items, total });
    }

    [HttpPost("admin/posts/{id}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApprovePost(int id, CancellationToken ct)
    {
        await _forum.ApprovePostAsync(id, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã duyệt bài." });
    }

    [HttpPost("admin/posts/{id}/reject")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectPost(int id, [FromBody] RejectRequest req, CancellationToken ct)
    {
        await _forum.RejectPostAsync(id, req.Reason, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã từ chối bài." });
    }

    [HttpPost("admin/posts/{id}/hide")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> HidePost(int id, CancellationToken ct)
    {
        await _forum.HidePostAsync(id, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã ẩn bài." });
    }

    [HttpPost("admin/comments/{id}/hide")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> HideComment(int id, CancellationToken ct)
    {
        await _forum.HideCommentAsync(id, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã ẩn bình luận." });
    }

    [HttpPut("comments/{id}")]
    [Authorize]
    public async Task<IActionResult> EditComment(int id, [FromBody] AddCommentRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _forum.EditCommentAsync(id, userId.Value, req.Content, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpDelete("comments/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int id, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _forum.DeleteCommentAsync(id, userId.Value, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpPost("admin/users/{userId}/ban-comment")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> BanComment(Guid userId, [FromBody] BanRequest req, CancellationToken ct)
    {
        await _forum.BanUserCommentAsync(userId, req.Reason, req.Days, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = $"Đã cấm bình luận {req.Days} ngày." });
    }

    [HttpPost("admin/users/{userId}/unban-comment")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UnbanComment(Guid userId, CancellationToken ct)
    {
        await _forum.UnbanUserCommentAsync(userId, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = "Đã gỡ cấm bình luận." });
    }
}

public record CreatePostRequest(string Title, string Content, string LeagueTag, List<string>? MediaUrls, List<string>? MediaTypes);
public record EditPostRequest(string Title, string Content, List<string>? MediaUrls, List<string>? MediaTypes);
public record AddCommentRequest(string Content, int? ParentCommentId);
public record ReactionRequest(string ReactionType);
public record RejectRequest(string Reason);
public record BanRequest(string Reason, int Days = 2);
