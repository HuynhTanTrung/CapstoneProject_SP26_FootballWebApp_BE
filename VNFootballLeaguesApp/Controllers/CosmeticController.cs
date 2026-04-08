using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
using VNFootballLeaguesApp.DTOs.Common;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/cosmetics")]
public class CosmeticController : ControllerBase
{
    private readonly CosmeticService _svc;
    private readonly IUserService _userService;

    public CosmeticController(CosmeticService svc, IUserService userService)
    {
        _svc = svc;
        _userService = userService;
    }

    [HttpGet("shop")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetShop(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var items = await _svc.GetShopAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = items });
    }

    [HttpGet("inventory")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> GetInventory(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var items = await _svc.GetInventoryAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = items });
    }

    [HttpPost("purchase/{itemId}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> Purchase(int itemId, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _svc.PurchaseAsync(userId.Value, itemId, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        // Check achievements after purchase
        await _svc.CheckAndUnlockAchievementsAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpPost("equip")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> Equip([FromBody] EquipRequest req, CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var (success, message) = await _svc.EquipAsync(userId.Value, req.FrameItemId, req.NameColorItemId,
            req.BannerItemId, req.BadgeItemId, req.EffectItemId, req.CardItemId, ct);
        if (!success) return BadRequest(new ApiResponseDto<object> { Success = false, Message = message });
        return Ok(new ApiResponseDto<object> { Success = true, Message = message });
    }

    [HttpGet("loadout/{userId}")]
    public async Task<IActionResult> GetLoadout(Guid userId, CancellationToken ct)
    {
        var loadout = await _svc.GetLoadoutAsync(userId, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = loadout });
    }

    [HttpGet("full-loadout/{userId}")]
    public async Task<IActionResult> GetFullLoadout(Guid userId, CancellationToken ct)
    {
        var loadout = await _svc.GetFullLoadoutAsync(userId, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = loadout });
    }

    [HttpPost("check-achievements")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<IActionResult> CheckAchievements(CancellationToken ct)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        await _svc.CheckAndUnlockAchievementsAsync(userId.Value, ct);
        var inventory = await _svc.GetInventoryAsync(userId.Value, ct);
        return Ok(new ApiResponseDto<object> { Success = true, Data = inventory });
    }
}

public class EquipRequest
{
    public int? FrameItemId { get; set; }
    public int? NameColorItemId { get; set; }
    public int? BannerItemId { get; set; }
    public int? BadgeItemId { get; set; }
    public int? EffectItemId { get; set; }
    public int? CardItemId { get; set; }
}
