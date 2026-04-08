using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Services;

public class CosmeticService
{
    private readonly VNFootballLeaguesDBContext _db;
    public CosmeticService(VNFootballLeaguesDBContext db) => _db = db;

    // ── DTOs ──────────────────────────────────────────────────────────────
    public record CosmeticItemDto(int ItemId, string Name, string? Description, string Category,
        string UnlockType, int? PointCost, string? AchievementKey, string? PreviewData, bool IsOwned);

    public record LoadoutDto(int? FrameItemId, int? NameColorItemId, int? BannerItemId,
        int? BadgeItemId, int? EffectItemId, int? CardItemId);

    // ── Shop ──────────────────────────────────────────────────────────────
    public async Task<List<CosmeticItemDto>> GetShopAsync(Guid userId, CancellationToken ct = default)
    {
        var owned = await _db.UserCosmetics.Where(uc => uc.UserId == userId).Select(uc => uc.ItemId).ToListAsync(ct);
        var items = await _db.CosmeticItems.Where(i => i.IsActive && i.UnlockType == "shop").ToListAsync(ct);
        return items.Select(i => new CosmeticItemDto(i.ItemId, i.Name, i.Description, i.Category,
            i.UnlockType, i.PointCost, i.AchievementKey, i.PreviewData, owned.Contains(i.ItemId))).ToList();
    }

    // ── Inventory ─────────────────────────────────────────────────────────
    public async Task<List<CosmeticItemDto>> GetInventoryAsync(Guid userId, CancellationToken ct = default)
    {
        var ownedIds = await _db.UserCosmetics.Where(uc => uc.UserId == userId).Select(uc => uc.ItemId).ToListAsync(ct);
        var items = await _db.CosmeticItems.Where(i => ownedIds.Contains(i.ItemId)).ToListAsync(ct);
        return items.Select(i => new CosmeticItemDto(i.ItemId, i.Name, i.Description, i.Category,
            i.UnlockType, i.PointCost, i.AchievementKey, i.PreviewData, true)).ToList();
    }

    // ── Purchase ──────────────────────────────────────────────────────────
    public async Task<(bool Success, string Message)> PurchaseAsync(Guid userId, int itemId, CancellationToken ct = default)
    {
        var item = await _db.CosmeticItems.FirstOrDefaultAsync(i => i.ItemId == itemId && i.IsActive && i.UnlockType == "shop", ct);
        if (item == null) return (false, "Không tìm thấy item.");

        if (await _db.UserCosmetics.AnyAsync(uc => uc.UserId == userId && uc.ItemId == itemId, ct))
            return (false, "Bạn đã sở hữu item này rồi.");

        var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        int currentPoints = stats?.Points ?? 0;
        if (currentPoints < item.PointCost!.Value)
            return (false, $"Không đủ điểm. Cần {item.PointCost}đ, bạn có {currentPoints}đ.");

        // Deduct points
        if (stats == null)
            return (false, "Không tìm thấy thông tin điểm.");
        stats.Points = currentPoints - item.PointCost.Value;
        stats.LastUpdated = DateTime.UtcNow;

        _db.UserCosmetics.Add(new UserCosmetic { UserId = userId, ItemId = itemId });
        await _db.SaveChangesAsync(ct);
        return (true, $"Mua thành công! Còn lại {stats.Points}đ.");
    }

    // ── Equip ─────────────────────────────────────────────────────────────
    public async Task<(bool Success, string Message)> EquipAsync(Guid userId, int? frameId, int? nameColorId,
        int? bannerId, int? badgeId, int? effectId, int? cardId, CancellationToken ct = default)
    {
        var ownedIds = await _db.UserCosmetics.Where(uc => uc.UserId == userId).Select(uc => uc.ItemId).ToListAsync(ct);

        // Validate all equipped items are owned
        var toCheck = new[] { frameId, nameColorId, bannerId, badgeId, effectId, cardId }.Where(id => id.HasValue).Select(id => id!.Value);
        if (toCheck.Any(id => !ownedIds.Contains(id)))
            return (false, "Bạn không sở hữu một trong các item này.");

        var loadout = await _db.UserLoadouts.FirstOrDefaultAsync(l => l.UserId == userId, ct);
        if (loadout == null)
        {
            _db.UserLoadouts.Add(new UserLoadout
            {
                UserId = userId, FrameItemId = frameId, NameColorItemId = nameColorId,
                BannerItemId = bannerId, BadgeItemId = badgeId, EffectItemId = effectId,
                CardItemId = cardId, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            loadout.FrameItemId = frameId;
            loadout.NameColorItemId = nameColorId;
            loadout.BannerItemId = bannerId;
            loadout.BadgeItemId = badgeId;
            loadout.EffectItemId = effectId;
            loadout.CardItemId = cardId;
            loadout.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return (true, "Đã cập nhật trang phục.");
    }

    // ── Loadout ───────────────────────────────────────────────────────────
    public async Task<LoadoutDto?> GetLoadoutAsync(Guid userId, CancellationToken ct = default)
    {
        var l = await _db.UserLoadouts.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (l == null) return new LoadoutDto(null, null, null, null, null, null);
        return new LoadoutDto(l.FrameItemId, l.NameColorItemId, l.BannerItemId, l.BadgeItemId, l.EffectItemId, l.CardItemId);
    }

    public record FullLoadoutDto(
        string? FramePreview, string? NameColorPreview, string? BannerPreview,
        string? BadgePreview, string? EffectPreview, string? CardPreview,
        string? FrameName, string? NameColorName, string? BadgeName);

    public async Task<FullLoadoutDto> GetFullLoadoutAsync(Guid userId, CancellationToken ct = default)
    {
        var l = await _db.UserLoadouts.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (l == null) return new FullLoadoutDto(null, null, null, null, null, null, null, null, null);

        var ids = new[] { l.FrameItemId, l.NameColorItemId, l.BannerItemId, l.BadgeItemId, l.EffectItemId, l.CardItemId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var items = await _db.CosmeticItems.Where(i => ids.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        string? Get(int? id) => id.HasValue && items.TryGetValue(id.Value, out var it) ? it.PreviewData : null;
        string? GetName(int? id) => id.HasValue && items.TryGetValue(id.Value, out var it) ? it.Name : null;

        return new FullLoadoutDto(
            Get(l.FrameItemId), Get(l.NameColorItemId), Get(l.BannerItemId),
            Get(l.BadgeItemId), Get(l.EffectItemId), Get(l.CardItemId),
            GetName(l.FrameItemId), GetName(l.NameColorItemId), GetName(l.BadgeItemId));
    }

    // ── Achievement unlock ────────────────────────────────────────────────
    public async Task CheckAndUnlockAchievementsAsync(Guid userId, CancellationToken ct = default)
    {
        var stats = await _db.UserPredictionStats.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var streak = await _db.DailyCheckIns.Where(c => c.UserId == userId).OrderByDescending(c => c.CheckInDate)
            .Select(c => c.Streak).FirstOrDefaultAsync(ct);

        var correctMatch = await _db.Predictions.CountAsync(p => p.UserId == userId && (p.IsCorrect ?? 0) > 0, ct);
        var exactMatch = await _db.Predictions.CountAsync(p => p.UserId == userId && p.IsCorrect == 2, ct);

        var keys = new List<string>();
        if (streak >= 7) keys.Add("streak_7");
        if (streak >= 30) keys.Add("streak_30");
        if (streak >= 100) keys.Add("streak_100");
        if (correctMatch >= 10) keys.Add("correct_10");
        if (exactMatch >= 10) keys.Add("exact_10");
        if (correctMatch >= 50) keys.Add("correct_50");

        if (!keys.Any()) return;

        var achievementItems = await _db.CosmeticItems
            .Where(i => i.UnlockType == "achievement" && i.AchievementKey != null && keys.Contains(i.AchievementKey))
            .ToListAsync(ct);

        var ownedIds = await _db.UserCosmetics.Where(uc => uc.UserId == userId).Select(uc => uc.ItemId).ToListAsync(ct);

        foreach (var item in achievementItems.Where(i => !ownedIds.Contains(i.ItemId)))
            _db.UserCosmetics.Add(new UserCosmetic { UserId = userId, ItemId = item.ItemId });

        await _db.SaveChangesAsync(ct);
    }
}
