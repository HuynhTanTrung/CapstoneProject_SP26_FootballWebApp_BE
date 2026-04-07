using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

/// <summary>
/// Dự đoán tỉ số: chọn trận → nhập tỉ số → submit. Đúng tỉ số: 3 điểm; đúng thắng/thua/hòa: 1 điểm. Huy hiệu theo tổng điểm: 1 / 100 / 150.
/// </summary>
[ApiController]
[Route("api/predictions")]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;
    private readonly IUserService _userService;

    public PredictionsController(IPredictionService predictionService, IUserService userService)
    {
        _predictionService = predictionService;
        _userService = userService;
    }

    /// <summary>Gửi hoặc cập nhật dự đoán cho một trận (trước khi trận bắt đầu / kết thúc).</summary>
    [HttpPost]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> Submit([FromBody] SubmitPredictionRequest request, CancellationToken cancellationToken)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(ApiFail("Không xác định được người dùng."));

        var result = await _predictionService.SubmitPredictionAsync(userId.Value, request, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiFail(result.Message));

        return Ok(new ApiResponseDto<PredictionItemDto?>
        {
            Success = true,
            Message = result.Message,
            Data = result.Prediction
        });
    }

    /// <summary>Danh sách dự đoán của tôi.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(ApiFail("Không xác định được người dùng."));

        var list = await _predictionService.GetMyPredictionsAsync(userId.Value, cancellationToken);
        return Ok(new ApiResponseDto<IReadOnlyList<PredictionItemDto>>
        {
            Success = true,
            Message = "OK",
            Data = list
        });
    }

    /// <summary>Bảng điểm tổng hợp dự đoán của tôi.</summary>
    [HttpGet("me/stats")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetMyStats(CancellationToken cancellationToken)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(ApiFail("Không xác định được người dùng."));

        var stats = await _predictionService.GetMyStatsAsync(userId.Value, cancellationToken);
        return Ok(new ApiResponseDto<UserPredictionStatsDto?>
        {
            Success = true,
            Message = "OK",
            Data = stats
        });
    }

    /// <summary>Dự đoán của tôi cho một trận cụ thể.</summary>
    [HttpGet("match/{matchId:int}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetForMatch([FromRoute] int matchId, CancellationToken cancellationToken)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(ApiFail("Không xác định được người dùng."));

        var p = await _predictionService.GetMyPredictionForMatchAsync(userId.Value, matchId, cancellationToken);
        if (p == null)
            return Ok(new ApiResponseDto<PredictionItemDto?>
            {
                Success = true,
                Message = "Chưa có dự đoán cho trận này.",
                Data = null
            });

        return Ok(new ApiResponseDto<PredictionItemDto?>
        {
            Success = true,
            Message = "OK",
            Data = p
        });
    }

    /// <summary>Danh sách phần thưởng (cấu hình trong bảng Reward).</summary>
    [HttpGet("rewards")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRewards(CancellationToken cancellationToken)
    {
        var list = await _predictionService.GetRewardsAsync(cancellationToken);
        return Ok(new ApiResponseDto<IReadOnlyList<RewardDto>>
        {
            Success = true,
            Message = "OK",
            Data = list
        });
    }

    /// <summary>Phần thưởng đã nhận của tôi.</summary>
    [HttpGet("my-rewards")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetMyRewards(CancellationToken cancellationToken)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(ApiFail("Không xác định được người dùng."));

        var list = await _predictionService.GetMyRewardsAsync(userId.Value, cancellationToken);
        return Ok(new ApiResponseDto<IReadOnlyList<UserRewardDto>>
        {
            Success = true,
            Message = "OK",
            Data = list
        });
    }

    /// <summary>Chấm điểm thủ công cho một trận (khi đã có tỉ số và trạng thái kết thúc trong DB).</summary>
    [HttpPost("admin/settle/{matchId:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SettleMatch([FromRoute] int matchId, CancellationToken cancellationToken)
    {
        var n = await _predictionService.SettleMatchAsync(matchId, cancellationToken);
        return Ok(new ApiResponseDto<int>
        {
            Success = true,
            Message = n == 0 ? "Không có dự đoán cần chấm hoặc trận chưa đủ điều kiện." : $"Đã chấm {n} dự đoán.",
            Data = n
        });
    }

    /// <summary>
    /// Tính lại điểm/thống kê và trao huy hiệu từ dữ liệu <c>Prediction</c> hiện có (test / dữ liệu import).
    /// </summary>
    [HttpPost("admin/recompute-stats/{userId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RecomputeStatsForUser([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        await _predictionService.RecomputeUserStatsAndBadgesAsync(userId, cancellationToken);
        var stats = await _predictionService.GetMyStatsAsync(userId, cancellationToken);
        var rewards = await _predictionService.GetMyRewardsAsync(userId, cancellationToken);
        return Ok(new ApiResponseDto<object>
        {
            Success = true,
            Message = "Đã tính lại stats và kiểm tra huy hiệu.",
            Data = new { stats, rewards }
        });
    }

    private static ApiResponseDto<object> ApiFail(string message) =>
        new() { Success = false, Message = message };
}
