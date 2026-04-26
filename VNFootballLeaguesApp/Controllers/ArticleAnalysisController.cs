using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/article-analysis")]
public class ArticleAnalysisController : ControllerBase
{
    private readonly IArticleAnalysisService _service;
    private readonly VNFootballLeaguesDBContext _db;

    public ArticleAnalysisController(IArticleAnalysisService service, VNFootballLeaguesDBContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>
    /// Analyze a football article using AI. Requires premium subscription.
    /// Called from Chrome Extension.
    /// </summary>
    [HttpPost("analyze")]
    [Authorize]
    public async Task<IActionResult> Analyze(
        [FromBody] ArticleAnalysisRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.ArticleContent))
            return BadRequest(new { message = "Nội dung bài viết không được để trống." });

        var result = await _service.AnalyzeArticleAsync(request, userId, ct);

        if (!result.Success && result.Analysis == "PREMIUM_REQUIRED")
            return StatusCode(403, new { message = result.Warning, code = "PREMIUM_REQUIRED" });

        if (!result.Success && result.Analysis == "NO_CREDITS")
            return StatusCode(403, new { message = result.Warning, code = "NO_CREDITS" });

        if (!result.Success && result.Analysis == "INVALID_CONTENT")
            return BadRequest(new { message = result.Warning });

        return Ok(result);
    }

    /// <summary>
    /// Check subscription status and credits for article analysis.
    /// Called by extension on popup open to show correct UI state.
    /// </summary>
    [HttpGet("check-access")]
    [Authorize]
    public async Task<IActionResult> CheckAccess()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var sub = await _db.UserSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        var isPremium = sub != null
            && sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
            && sub.ExpiresAt > DateTime.UtcNow;

        var creditsRemaining = isPremium ? sub!.AiArticleCreditsRemaining : 0;
        var hasCredits = isPremium && creditsRemaining > 0;

        return Ok(new { isPremium, hasCredits, creditsRemaining });
    }
}
