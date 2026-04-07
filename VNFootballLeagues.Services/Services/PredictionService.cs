using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;

namespace VNFootballLeagues.Services.Services;

public class PredictionService : IPredictionService
{
    /// <summary>Danh hiệu theo <b>tổng điểm</b> dự đoán (tổng cột Points trên các trận đã chấm).</summary>
    private static readonly (string Name, string Description, int RequiredTotalPoints, string IconUrl)[] PredictionBadgeTiers =
    [
        ("Bronze Badge", "Đạt tổng 1 điểm", 1,
            "https://png.pngtree.com/png-clipart/20250424/original/pngtree-d-isolated-render-of-a-bronze-badge-icon-featuring-sleek-metallic-png-image_20786549.png"),
        ("Silver Badge", "Đạt tổng 100 điểm", 100,
            "https://png.pngtree.com/png-vector/20250401/ourlarge/pngtree-d-isolated-render-of-a-silver-badge-icon-featuring-sleek-metallic-png-image_15917465.png"),
        ("Gold Badge", "Đạt tổng 150 điểm", 150,
            "https://png.pngtree.com/png-clipart/20230522/ourmid/pngtree-luxury-gold-badge-png-image_7104757.png"),
    ];

    private readonly VNFootballLeaguesDBContext _db;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(VNFootballLeaguesDBContext db, ILogger<PredictionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PredictionSubmitResult> SubmitPredictionAsync(Guid userId, SubmitPredictionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PredictedHomeGoals < 0 || request.PredictedAwayGoals < 0)
            return Fail("Tỉ số không hợp lệ.");

        var match = await _db.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MatchId == request.MatchId, cancellationToken);

        if (match == null)
            return Fail("Không tìm thấy trận đấu.");

        if (!MatchAllowsPrediction(match.Status))
            return Fail("Trận đã diễn ra hoặc đã kết thúc, không thể dự đoán.");

        var existing = await _db.Predictions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.MatchId == request.MatchId, cancellationToken);

        if (existing != null)
            return Fail("Bạn đã dự đoán trận này rồi. Chỉ được vote 1 lần.");

        if (existing == null)
        {
            // EnableRetryOnFailure bắt buộc bọc transaction trong CreateExecutionStrategy (không gọi BeginTransaction trực tiếp ngoài strategy).
            PredictionSubmitResult? created = null;
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var nextId = await _db.Predictions.AnyAsync(cancellationToken)
                        ? await _db.Predictions.MaxAsync(p => p.PredictionId, cancellationToken) + 1
                        : 1;

                    var row = new Prediction
                    {
                        PredictionId = nextId,
                        UserId = userId,
                        MatchId = request.MatchId,
                        PredictedHomeGoals = request.PredictedHomeGoals,
                        PredictedAwayGoals = request.PredictedAwayGoals,
                        CreatedAt = DateTime.UtcNow,
                        Points = null,
                        IsCorrect = null
                    };

                    _db.Predictions.Add(row);
                    await BumpTotalPredictionsAsync(userId, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    created = new PredictionSubmitResult
                    {
                        Success = true,
                        Message = "Đã gửi dự đoán.",
                        Prediction = await MapToItemAsync(row.PredictionId, cancellationToken)
                    };
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Submit prediction failed");
                    created = Fail("Không thể lưu dự đoán. Thử lại sau.");
                }
            });

            return created ?? Fail("Không thể lưu dự đoán. Thử lại sau.");
        }

        existing.PredictedHomeGoals = request.PredictedHomeGoals;
        existing.PredictedAwayGoals = request.PredictedAwayGoals;
        existing.CreatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new PredictionSubmitResult
        {
            Success = true,
            Message = "Đã cập nhật dự đoán.",
            Prediction = await MapToItemAsync(existing.PredictionId, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<PredictionItemDto>> GetMyPredictionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Include(p => p.Match!)
                .ThenInclude(m => m.HomeTeam)
            .Include(p => p.Match!)
                .ThenInclude(m => m.AwayTeam)
            .ToListAsync(cancellationToken);

        return rows
            .Where(p => p.Match != null)
            .Select(p => new PredictionItemDto
            {
                PredictionId = p.PredictionId,
                MatchId = p.MatchId ?? 0,
                HomeTeamName = p.Match!.HomeTeam?.TeamName,
                AwayTeamName = p.Match.AwayTeam?.TeamName,
                PredictedHomeGoals = p.PredictedHomeGoals,
                PredictedAwayGoals = p.PredictedAwayGoals,
                ActualHomeGoals = p.Match.HomeGoals,
                ActualAwayGoals = p.Match.AwayGoals,
                MatchStatus = p.Match.Status,
                IsCorrect = p.IsCorrect,
                Points = p.Points,
                CreatedAt = p.CreatedAt
            })
            .ToList();
    }

    public async Task<PredictionItemDto?> GetMyPredictionForMatchAsync(Guid userId, int matchId, CancellationToken cancellationToken = default)
    {
        var p = await _db.Predictions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.MatchId == matchId)
            .Include(x => x.Match!)
                .ThenInclude(m => m.HomeTeam)
            .Include(x => x.Match!)
                .ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(cancellationToken);

        if (p?.Match == null)
            return null;

        return new PredictionItemDto
        {
            PredictionId = p.PredictionId,
            MatchId = p.MatchId ?? 0,
            HomeTeamName = p.Match.HomeTeam?.TeamName,
            AwayTeamName = p.Match.AwayTeam?.TeamName,
            PredictedHomeGoals = p.PredictedHomeGoals,
            PredictedAwayGoals = p.PredictedAwayGoals,
            ActualHomeGoals = p.Match.HomeGoals,
            ActualAwayGoals = p.Match.AwayGoals,
            MatchStatus = p.Match.Status,
            IsCorrect = p.IsCorrect,
            Points = p.Points,
            CreatedAt = p.CreatedAt
        };
    }

    public async Task<UserPredictionStatsDto?> GetMyStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserPredictionStats.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (row == null)
        {
            return new UserPredictionStatsDto();
        }

        return new UserPredictionStatsDto
        {
            TotalPredictions = row.TotalPredictions ?? 0,
            CorrectPredictions = row.CorrectPredictions ?? 0,
            ExactScorePredictions = row.ExactScorePredictions ?? 0,
            Points = row.Points ?? 0,
            LastUpdated = row.LastUpdated
        };
    }

    public async Task<IReadOnlyList<RewardDto>> GetRewardsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Rewards
            .AsNoTracking()
            .OrderBy(r => r.RewardId)
            .Select(r => new RewardDto
            {
                RewardId = r.RewardId,
                RewardName = r.RewardName,
                Description = r.Description,
                RequiredCorrectPredictions = r.RequiredCorrectPredictions,
                IconUrl = r.IconUrl
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserRewardDto>> GetMyRewardsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserRewards
            .AsNoTracking()
            .Include(ur => ur.Reward)
            .Where(ur => ur.UserId == userId)
            .OrderByDescending(ur => ur.AwardedAt)
            .Select(ur => new UserRewardDto
            {
                UserRewardId = ur.UserRewardId,
                AwardedAt = ur.AwardedAt,
                RewardId = ur.RewardId,
                RewardName = ur.Reward != null ? ur.Reward.RewardName : null,
                Description = ur.Reward != null ? ur.Reward.Description : null,
                IconUrl = ur.Reward != null ? ur.Reward.IconUrl : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SettleMatchAsync(int matchId, CancellationToken cancellationToken = default)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.MatchId == matchId, cancellationToken);
        if (match == null)
            return 0;

        if (!MatchHasFinalScore(match))
            return 0;

        var ah = match.HomeGoals!.Value;
        var aa = match.AwayGoals!.Value;

        var preds = await _db.Predictions
            .Where(p => p.MatchId == matchId && p.Points == null)
            .ToListAsync(cancellationToken);

        if (preds.Count == 0)
            return 0;

        foreach (var p in preds)
        {
            if (p.PredictedHomeGoals == null || p.PredictedAwayGoals == null)
                continue;

            var (scorePoints, level) = Score(
                p.PredictedHomeGoals.Value,
                p.PredictedAwayGoals.Value,
                ah,
                aa);

            p.Points = scorePoints;
            p.IsCorrect = level;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var userIds = preds.Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct();
        foreach (var uid in userIds)
            await RecalculateUserStatsAsync(uid, cancellationToken);

        return preds.Count;
    }

    public Task RecomputeUserStatsAndBadgesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RecalculateUserStatsAsync(userId, cancellationToken);

    public async Task<int> SettleAllPendingAsync(CancellationToken cancellationToken = default)
    {
        var matchIds = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.Points == null && p.MatchId != null)
            .Select(p => p.MatchId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var total = 0;
        foreach (var mid in matchIds)
        {
            var n = await SettleMatchAsync(mid, cancellationToken);
            total += n;
        }

        return total;
    }

    private async Task BumpTotalPredictionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (stats == null)
        {
            _db.UserPredictionStats.Add(new UserPredictionStats
            {
                UserId = userId,
                TotalPredictions = 1,
                CorrectPredictions = 0,
                ExactScorePredictions = 0,
                Points = 0,
                LastUpdated = DateTime.UtcNow
            });
        }
        else
        {
            stats.TotalPredictions = (stats.TotalPredictions ?? 0) + 1;
            stats.LastUpdated = DateTime.UtcNow;
        }
    }

    private async Task RecalculateUserStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Match predictions
        var settled = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Points != null)
            .ToListAsync(cancellationToken);

        var totalMatchPred = await _db.Predictions.CountAsync(p => p.UserId == userId, cancellationToken);
        var matchPoints = settled.Sum(p => p.Points ?? 0);
        var correct = settled.Count(p => (p.Points ?? 0) > 0);
        var exact = settled.Count(p => p.Points == 3);

        // Contest entries
        var contestEntries = await _db.ContestEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var totalContests = await _db.ContestEntries
            .Where(e => e.UserId == userId)
            .Select(e => e.ContestId)
            .Distinct()
            .CountAsync(cancellationToken);

        var contestPoints = contestEntries.Sum(e => e.Points ?? 0);
        var contestCorrect = contestEntries.Count(e => (e.IsCorrect ?? 0) > 0);

        var totalPred = totalMatchPred + totalContests;
        var points = matchPoints + contestPoints;
        correct += contestCorrect;

        var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (stats == null)
        {
            _db.UserPredictionStats.Add(new UserPredictionStats
            {
                UserId = userId,
                TotalPredictions = totalPred,
                CorrectPredictions = correct,
                ExactScorePredictions = exact,
                Points = points,
                LastUpdated = DateTime.UtcNow
            });
        }
        else
        {
            stats.TotalPredictions = totalPred;
            stats.CorrectPredictions = correct;
            stats.ExactScorePredictions = exact;
            stats.Points = points;
            stats.LastUpdated = DateTime.UtcNow;
        }

        await EnsurePredictionBadgeRewardsInDatabaseAsync(cancellationToken);
        await TryAwardPredictionBadgesAsync(userId, points, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Seed / cập nhật 3 Reward (ngưỡng điểm lưu trong RequiredCorrectPredictions — cột DB giữ tên cũ).</summary>
    private async Task EnsurePredictionBadgeRewardsInDatabaseAsync(CancellationToken cancellationToken)
    {
        var changed = false;

        foreach (var (name, description, requiredPoints, iconUrl) in PredictionBadgeTiers)
        {
            var row = await _db.Rewards.FirstOrDefaultAsync(r => r.RewardName == name, cancellationToken);
            if (row != null)
            {
                if (row.Description != description || row.RequiredCorrectPredictions != requiredPoints || row.IconUrl != iconUrl)
                {
                    row.Description = description;
                    row.RequiredCorrectPredictions = requiredPoints;
                    row.IconUrl = iconUrl;
                    changed = true;
                }

                continue;
            }

            _db.Rewards.Add(new Reward
            {
                RewardName = name,
                Description = description,
                RequiredCorrectPredictions = requiredPoints,
                IconUrl = iconUrl,
                CreatedAt = DateTime.UtcNow
            });
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Khi <b>tổng điểm</b> đạt ngưỡng 1 / 100 / 150, ghi nhận UserReward (mỗi danh hiệu một lần).</summary>
    private async Task TryAwardPredictionBadgesAsync(Guid userId, int totalPoints, CancellationToken cancellationToken)
    {
        var nextUserRewardId = await _db.UserRewards.AnyAsync(cancellationToken)
            ? await _db.UserRewards.MaxAsync(ur => ur.UserRewardId, cancellationToken) + 1
            : 1;

        foreach (var (name, _, requiredPoints, _) in PredictionBadgeTiers)
        {
            if (totalPoints < requiredPoints)
                continue;

            var reward = await _db.Rewards.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RewardName == name, cancellationToken);
            if (reward == null)
                continue;

            var already = await _db.UserRewards
                .AnyAsync(ur => ur.UserId == userId && ur.RewardId == reward.RewardId, cancellationToken);
            if (already)
                continue;

            _db.UserRewards.Add(new UserReward
            {
                UserRewardId = nextUserRewardId++,
                UserId = userId,
                RewardId = reward.RewardId,
                AwardedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "User {UserId} đạt danh hiệu {Badge} (tổng điểm ≥ {Required}).",
                userId, name, requiredPoints);
        }
    }

    private async Task<PredictionItemDto?> MapToItemAsync(int predictionId, CancellationToken cancellationToken)
    {
        var p = await _db.Predictions
            .AsNoTracking()
            .Include(x => x.Match)
                .ThenInclude(m => m!.HomeTeam)
            .Include(x => x.Match)
                .ThenInclude(m => m!.AwayTeam)
            .FirstOrDefaultAsync(x => x.PredictionId == predictionId, cancellationToken);

        if (p?.Match == null)
            return null;

        return new PredictionItemDto
        {
            PredictionId = p.PredictionId,
            MatchId = p.MatchId ?? 0,
            HomeTeamName = p.Match.HomeTeam?.TeamName,
            AwayTeamName = p.Match.AwayTeam?.TeamName,
            PredictedHomeGoals = p.PredictedHomeGoals,
            PredictedAwayGoals = p.PredictedAwayGoals,
            ActualHomeGoals = p.Match.HomeGoals,
            ActualAwayGoals = p.Match.AwayGoals,
            MatchStatus = p.Match.Status,
            IsCorrect = p.IsCorrect,
            Points = p.Points,
            CreatedAt = p.CreatedAt
        };
    }

    private static PredictionSubmitResult Fail(string message) =>
        new() { Success = false, Message = message };

    /// <summary>Đúng tỉ số: 3 điểm (IsCorrect=2). Đúng thắng/thua/hòa: 1 điểm (IsCorrect=1). Sai: 0.</summary>
    private static (int points, int isCorrectLevel) Score(int ph, int pa, int ah, int aa)
    {
        if (ph == ah && pa == aa)
            return (3, 2);

        static int Outcome(int h, int a) => h > a ? 1 : (h < a ? -1 : 0);

        if (Outcome(ph, pa) == Outcome(ah, aa))
            return (1, 1);

        return (0, 0);
    }

    public static bool MatchAllowsPrediction(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        var u = status.Trim().ToUpperInvariant();
        if (u is "FT" or "FINISHED" or "AET" or "PEN" or "AWD" or "WO")
            return false;
        if (u.Contains("FINISH", StringComparison.OrdinalIgnoreCase))
            return false;
        if (u is "LIVE" or "1H" or "2H" or "HT" or "IN_PLAY" or "ET" or "P" or "BT" or "INT")
            return false;
        if (u is "CANC" or "PST" or "ABD" or "SUSP" or "INTERRUPTED")
            return false;

        return true;
    }

    /// <summary>
    /// Cho phép chấm điểm khi đã có tỉ số hai đội. Nếu chỉ cập nhật bàn thắng trong DB mà chưa đổi Status sang FT,
    /// vẫn chấm được — trừ khi status cho thấy trận đang diễn ra.
    /// </summary>
    public static bool MatchHasFinalScore(Match m)
    {
        if (m.HomeGoals == null || m.AwayGoals == null)
            return false;

        var s = m.Status?.Trim().ToUpperInvariant() ?? "";

        // Trận đang đá — chưa coi là kết quả cuối để chấm dự đoán
        if (s is "LIVE" or "1H" or "2H" or "HT" or "IN_PLAY" or "ET" or "P" or "BT" or "INT")
            return false;

        if (s is "FT" or "FINISHED" or "AET" or "PEN" or "AWD")
            return true;
        if (s.Contains("FINISH", StringComparison.OrdinalIgnoreCase))
            return true;

        // Có đủ tỉ số nhưng Status còn NS/TBD/trống... (thường gặp khi sửa tay) — vẫn cho chấm
        return true;
    }
}
