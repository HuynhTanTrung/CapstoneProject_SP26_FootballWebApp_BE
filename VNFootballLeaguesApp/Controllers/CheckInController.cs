using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.Services;
using VNFootballLeagues.Services.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/checkin")]
[Authorize(Policy = "UserOrAdmin")]
public class CheckInController : ControllerBase
{
    private readonly CheckInService _checkInService;
    private readonly IUserService _userService;

    public CheckInController(CheckInService checkInService, IUserService userService)
    {
        _checkInService = checkInService;
        _userService = userService;
    }

    /// <summary>Điểm danh hôm nay để nhận điểm.</summary>
    [HttpPost]
    public async Task<IActionResult> CheckIn(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized(ApiFail("Không xác định được người dùng."));

        var result = await _checkInService.CheckInAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<CheckInResultDto> { Success = true, Data = result });
    }

    /// <summary>Lấy trạng thái điểm danh: chuỗi hiện tại, tổng điểm, ngày đã điểm danh trong tháng.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized(ApiFail("Không xác định được người dùng."));

        var status = await _checkInService.GetStatusAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<CheckInStatusDto> { Success = true, Data = status });
    }

    private static ApiResponseDto<object> ApiFail(string msg) =>
        new() { Success = false, Message = msg };
}
