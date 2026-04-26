using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/leaderboard")]
public class LeaderboardController : ControllerBase
{
    private readonly IMonthlyPredictionLeaderboardService _monthlyLeaderboardService;

    public LeaderboardController(IMonthlyPredictionLeaderboardService monthlyLeaderboardService)
    {
        _monthlyLeaderboardService = monthlyLeaderboardService;
    }

    /// <summary>
    /// Bảng xếp hạng điểm dự đoán theo tháng (dự đoán trận + dự đoán đặc biệt),
    /// chỉ cộng điểm ở thời điểm user có gói MONTHLY/QUARTERLY còn hạn.
    /// </summary>
    [HttpGet("predictions/monthly")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetMonthlyPredictionLeaderboard([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        try
        {
            var data = await _monthlyLeaderboardService.GetMonthlyLeaderboardAsync(year, month, ct);
            return Ok(new ApiResponseDto<MonthlyPredictionLeaderboardDto>
            {
                Success = true,
                Message = "OK",
                Data = data
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Chạy thưởng top 1-3 của tháng trước (Top1 +200, Top2 +150, Top3 +100).
    /// Endpoint hỗ trợ admin trigger thủ công khi cần.
    /// </summary>
    [HttpPost("/api/admin/leaderboard/predictions/monthly/reward-previous-month")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RewardPreviousMonth(CancellationToken ct)
    {
        var data = await _monthlyLeaderboardService.RewardTopUsersForPreviousMonthAsync(ct);
        return Ok(new ApiResponseDto<MonthlyLeaderboardRewardResultDto>
        {
            Success = true,
            Message = data.SkippedBecauseAlreadyRewarded
                ? "Tháng trước đã được trao thưởng trước đó."
                : "Đã xử lý trao thưởng top tháng trước.",
            Data = data
        });
    }
}
