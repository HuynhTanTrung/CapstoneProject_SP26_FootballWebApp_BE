using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/contests")]
public class ContestController : ControllerBase
{
    private readonly IContestService _contestService;
    private readonly IUserService _userService;

    public ContestController(IContestService contestService, IUserService userService)
    {
        _contestService = contestService;
        _userService = userService;
    }

    /// <summary>Danh sách contest đang mở (public).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetOpen(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        var list = await _contestService.GetOpenContestsAsync(userId, ct);
        return Ok(new ApiResponseDto<List<ContestDto>> { Success = true, Message = "OK", Data = list });
    }

    /// <summary>Chi tiết 1 contest.</summary>
    [HttpGet("{contestId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOne(int contestId, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        var dto = await _contestService.GetContestAsync(contestId, userId, ct);
        if (dto == null) return NotFound();
        return Ok(new ApiResponseDto<ContestDto> { Success = true, Message = "OK", Data = dto });
    }

    /// <summary>Submit dự đoán cho contest.</summary>
    [HttpPost("entry")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> Submit([FromBody] SubmitContestEntryRequest request, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId == null) return Unauthorized();
        var (success, message) = await _contestService.SubmitEntryAsync(userId.Value, request, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    /// <summary>Danh sách đội để chọn trong picker.</summary>
    [HttpGet("teams")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTeams([FromQuery] int? leagueId, [FromQuery] int? seasonId, CancellationToken ct)
    {
        var list = await _contestService.GetTeamsForPickerAsync(leagueId, seasonId, ct);
        return Ok(new ApiResponseDto<List<TeamPickerDto>> { Success = true, Message = "OK", Data = list });
    }

    /// <summary>Danh sách cầu thủ của 1 đội để chọn trong picker.</summary>
    [HttpGet("players/{teamId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlayers(int teamId, CancellationToken ct)
    {
        var list = await _contestService.GetPlayersForPickerAsync(teamId, ct);
        return Ok(new ApiResponseDto<List<PlayerPickerDto>> { Success = true, Message = "OK", Data = list });
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>Tạo contest mới (admin).</summary>
    [HttpPost("admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateContestRequest request, CancellationToken ct)
    {
        var dto = await _contestService.CreateContestAsync(request, ct);
        return Ok(new ApiResponseDto<ContestDto> { Success = true, Message = "Đã tạo contest.", Data = dto });
    }

    /// <summary>Danh sách tất cả contest (admin).</summary>
    [HttpGet("admin/all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _contestService.GetAllContestsAsync(ct);
        return Ok(new ApiResponseDto<List<ContestDto>> { Success = true, Message = "OK", Data = list });
    }

    /// <summary>Chấm điểm contest (admin nhập kết quả chính thức).</summary>
    [HttpPost("admin/settle")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Settle([FromBody] SettleContestRequest request, CancellationToken ct)
    {
        var (success, message, settled) = await _contestService.SettleContestAsync(request, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message, Data = new { settled } });
    }

    /// <summary>Danh sách contest đã kết thúc của user hiện tại.</summary>
    [HttpGet("settled")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetSettled(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId == null) return Unauthorized();
        var list = await _contestService.GetSettledContestsForUserAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<List<ContestDto>> { Success = true, Message = "OK", Data = list });
    }

    /// <summary>Chi tiết entries của 1 contest đã kết thúc (admin).</summary>
    [HttpGet("admin/{contestId:int}/entries")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetEntries(int contestId, CancellationToken ct)
    {
        var entries = await _contestService.GetContestEntriesAsync(contestId, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = entries });
    }
}
