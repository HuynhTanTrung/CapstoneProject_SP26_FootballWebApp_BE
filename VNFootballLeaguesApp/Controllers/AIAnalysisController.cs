using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/ai-analysis")]
[Authorize]
public class AIAnalysisController : ControllerBase
{
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IUserService _userService;
    private readonly VNFootballLeaguesDBContext _db;
    private readonly ILogger<AIAnalysisController> _logger;

    public AIAnalysisController(IAIAnalysisService aiAnalysisService, IUserService userService, VNFootballLeaguesDBContext db, ILogger<AIAnalysisController> logger)
    {
        _aiAnalysisService = aiAnalysisService;
        _userService = userService;
        _db = db;
        _logger = logger;
    }

    [HttpPost("player-rating")]
    public async Task<IActionResult> AnalyzePlayerRating([FromBody] PlayerRatingAnalysisRequest request)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        // Check credit
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId.Value);
        if (sub == null || sub.AiMatchAnalysisRemaining <= 0)
            return BadRequest(new { success = false, message = "Bạn đã hết lượt AI Phân tích. Vui lòng nâng cấp gói để tiếp tục." });

        try
        {
            var result = await _aiAnalysisService.AnalyzePlayerRatingAsync(request.MatchId, request.PlayerId, userId.Value, HttpContext.RequestAborted);
            if (!result.Success) return StatusCode(502, new { success = false, message = result.AnalysisVi });
            // Refresh sub to get updated credit
            var updatedSub = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId.Value);
            return Ok(new { result.Success, result.Mode, result.AnalysisVi, result.Context, result.Warning, creditsRemaining = updatedSub?.AiMatchAnalysisRemaining ?? 0 });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI player rating analysis failed for MatchId={MatchId}, PlayerId={PlayerId}", request.MatchId, request.PlayerId);
            return StatusCode(502, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("match")]
    public async Task<IActionResult> AnalyzeMatch([FromBody] MatchAnalysisRequest request)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        // Check credit
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId.Value);
        if (sub == null || sub.AiMatchAnalysisRemaining <= 0)
            return BadRequest(new { success = false, message = "Bạn đã hết lượt AI Phân tích. Vui lòng nâng cấp gói để tiếp tục." });

        try
        {
            var result = await _aiAnalysisService.AnalyzeMatchAsync(request.MatchId, userId.Value, HttpContext.RequestAborted);
            if (!result.Success) return StatusCode(502, new { success = false, message = result.AnalysisVi });
            var updatedSub = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId.Value);
            return Ok(new { result.Success, result.Mode, result.AnalysisVi, result.Context, result.Warning, creditsRemaining = updatedSub?.AiMatchAnalysisRemaining ?? 0 });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI match analysis failed for MatchId={MatchId}", request.MatchId);
            return StatusCode(502, new { success = false, message = ex.Message });
        }
    }

    /// <summary>Lấy lịch sử phân tích AI của user hiện tại.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? type = null)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        // type can be comma-separated: "player-rating,match"
        var types = type?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var history = await _aiAnalysisService.GetUserHistoryAsync(userId.Value, page, pageSize, types);
        return Ok(new { data = history, page, pageSize });
    }

    /// <summary>Lấy số lượt AI Match Analysis còn lại.</summary>
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var sub = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId.Value);
        return Ok(new { creditsRemaining = sub?.AiMatchAnalysisRemaining ?? 0 });
    }
}
