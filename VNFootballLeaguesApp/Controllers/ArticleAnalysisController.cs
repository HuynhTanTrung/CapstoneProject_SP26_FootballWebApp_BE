using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticleAnalysisController : ControllerBase
{
    private readonly IArticleAnalysisService _service;

    public ArticleAnalysisController(IArticleAnalysisService service)
    {
        _service = service;
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
    /// Check if current user has premium access for article analysis.
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

        // Delegate to service — reuse same subscription check
        var dummyRequest = new ArticleAnalysisRequest("", "", new string('x', 100));
        var result = await _service.AnalyzeArticleAsync(dummyRequest, userId);

        var isPremium = result.Analysis != "PREMIUM_REQUIRED";
        var hasCredits = result.Analysis != "NO_CREDITS" && isPremium;

        return Ok(new { isPremium, hasCredits });
    }
}
