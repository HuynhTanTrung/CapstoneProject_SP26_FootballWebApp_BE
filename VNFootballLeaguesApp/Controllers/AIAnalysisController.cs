using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;

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

    private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private static DateTime TodayVN() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz).Date;

    public AIAnalysisController(IAIAnalysisService aiAnalysisService, IUserService userService, VNFootballLeaguesDBContext db, ILogger<AIAnalysisController> logger)
    {
        _aiAnalysisService = aiAnalysisService;
        _userService = userService;
        _db = db;
        _logger = logger;
    }

    private async Task<(bool allowed, int limit, int used, IActionResult? error)> CheckDailyLimitAsync(Guid userId)
    {
        var today = TodayVN();
        var sub = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
        var isActive = sub != null && sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && sub.ExpiresAt > DateTime.UtcNow;
        var limit = SubscriptionCredits.GetDailyAiAnalysisLimit(sub?.PlanCode, isActive);

        var usage = await _db.DailyAiAnalysisUsages.FirstOrDefaultAsync(u => u.UserId == userId && u.UsageDate == today);
        int used = usage?.Count ?? 0;

        if (used >= limit)
        {
            var planLabel = isActive ? sub!.PlanName : "Free";
            return (false, limit, used, BadRequest(new
            {
                success = false,
                message = $"Bạn đã dùng hết {limit} lượt AI Phân tích hôm nay ({planLabel}). Vui lòng quay lại vào ngày mai hoặc nâng cấp gói.",
                limitReached = true,
                limit,
                used
            }));
        }

        return (true, limit, used, null);
    }

    private async Task IncrementDailyUsageAsync(Guid userId)
    {
        var today = TodayVN();
        var usage = await _db.DailyAiAnalysisUsages.FirstOrDefaultAsync(u => u.UserId == userId && u.UsageDate == today);
        if (usage == null)
            _db.DailyAiAnalysisUsages.Add(new DailyAiAnalysisUsage { UserId = userId, UsageDate = today, Count = 1 });
        else
            usage.Count++;
        await _db.SaveChangesAsync();
    }

    [HttpPost("player-rating")]
    public async Task<IActionResult> AnalyzePlayerRating([FromBody] PlayerRatingAnalysisRequest request)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var (allowed, limit, used, error) = await CheckDailyLimitAsync(userId.Value);
        if (!allowed) return error!;

        try
        {
            var result = await _aiAnalysisService.AnalyzePlayerRatingAsync(request.MatchId, request.PlayerId, userId.Value, HttpContext.RequestAborted);
            if (!result.Success) return StatusCode(502, new { success = false, message = result.AnalysisVi });

            await IncrementDailyUsageAsync(userId.Value);
            return Ok(new { result.Success, result.Mode, result.AnalysisVi, result.Context, result.Warning, dailyUsed = used + 1, dailyLimit = limit });
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

        var (allowed, limit, used, error) = await CheckDailyLimitAsync(userId.Value);
        if (!allowed) return error!;

        try
        {
            var result = await _aiAnalysisService.AnalyzeMatchAsync(request.MatchId, userId.Value, HttpContext.RequestAborted);
            if (!result.Success) return StatusCode(502, new { success = false, message = result.AnalysisVi });

            await IncrementDailyUsageAsync(userId.Value);
            return Ok(new { result.Success, result.Mode, result.AnalysisVi, result.Context, result.Warning, dailyUsed = used + 1, dailyLimit = limit });
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
        var types = type?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var history = await _aiAnalysisService.GetUserHistoryAsync(userId.Value, page, pageSize, types);
        return Ok(new { data = history, page, pageSize });
    }

    /// <summary>Lấy số lượt AI phân tích còn lại trong ngày.</summary>
    [HttpGet("daily-limit")]
    public async Task<IActionResult> GetDailyLimit()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();

        var today = TodayVN();
        var sub = await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId.Value);
        var isActive = sub != null && sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && sub.ExpiresAt > DateTime.UtcNow;
        var limit = SubscriptionCredits.GetDailyAiAnalysisLimit(sub?.PlanCode, isActive);
        var usage = await _db.DailyAiAnalysisUsages.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId.Value && u.UsageDate == today);
        int used = usage?.Count ?? 0;

        return Ok(new { limit, used, remaining = Math.Max(0, limit - used) });
    }
}
