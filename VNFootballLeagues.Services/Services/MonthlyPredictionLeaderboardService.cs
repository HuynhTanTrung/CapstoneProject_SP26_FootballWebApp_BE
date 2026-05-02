using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;
using VNFootballLeagues.Services.Models.Subscriptions;

namespace VNFootballLeagues.Services.Services;

public class MonthlyPredictionLeaderboardService : IMonthlyPredictionLeaderboardService
{
    private sealed record PaymentGrant(Guid UserId, DateTime PaidAt, int DurationDays);
    private sealed record PointEvent(Guid UserId, DateTime EventAt, int Points);

    private static readonly string[] EligiblePlanCodes = ["MONTHLY", "QUARTERLY"];
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    private readonly VNFootballLeaguesDBContext _db;
    private readonly NotificationService _notificationService;

    public MonthlyPredictionLeaderboardService(
        VNFootballLeaguesDBContext db,
        NotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    public async Task<MonthlyPredictionLeaderboardDto> GetMonthlyLeaderboardAsync(int? year = null, int? month = null, CancellationToken ct = default)
    {
        var (targetYear, targetMonth) = ResolveYearMonth(year, month);
        var (periodStartUtc, periodEndUtc) = GetUtcMonthRange(targetYear, targetMonth);

        var paidSubscriptionsRaw = await _db.SubscriptionPayments
            .AsNoTracking()
            .Where(p => p.Status == SubscriptionPaymentStatuses.Paid &&
                        p.PaidAt != null &&
                        EligiblePlanCodes.Contains(p.PlanCode))
            .OrderBy(p => p.UserId)
            .ThenBy(p => p.PaidAt)
            .Select(p => new
            {
                p.UserId,
                PaidAt = p.PaidAt!.Value,
                p.DurationDays
            })
            .ToListAsync(ct);
        var paidSubscriptions = paidSubscriptionsRaw
            .Select(p => new PaymentGrant(p.UserId, p.PaidAt, p.DurationDays))
            .ToList();

        var eligibleUserIds = paidSubscriptions.Select(p => p.UserId).Distinct().ToList();
        if (eligibleUserIds.Count == 0)
        {
            return new MonthlyPredictionLeaderboardDto
            {
                Year = targetYear,
                Month = targetMonth,
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                Rankings = []
            };
        }

        var activeWindowsByUser = BuildActiveWindowsByUser(paidSubscriptions);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => eligibleUserIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FullName,
                u.AvatarUrl
            })
            .ToListAsync(ct);

        var matchPointsRows = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.UserId != null &&
                        p.Points != null &&
                        p.CreatedAt != null &&
                        p.CreatedAt >= periodStartUtc &&
                        p.CreatedAt < periodEndUtc &&
                        eligibleUserIds.Contains(p.UserId.Value))
            .Select(p => new PointEvent(
                p.UserId!.Value,
                p.CreatedAt!.Value,
                p.Points!.Value))
            .ToListAsync(ct);

        var specialPointsRows = await _db.ContestEntries
            .AsNoTracking()
            .Where(e => e.Points != null &&
                        e.Contest != null &&
                        e.Contest.ResultAt != null &&
                        e.Contest.ResultAt >= periodStartUtc &&
                        e.Contest.ResultAt < periodEndUtc &&
                        eligibleUserIds.Contains(e.UserId))
            .Select(e => new PointEvent(
                e.UserId,
                e.Contest!.ResultAt!.Value,
                e.Points!.Value))
            .ToListAsync(ct);

        var matchByUser = AggregatePointsByUser(matchPointsRows, activeWindowsByUser);
        var specialByUser = AggregatePointsByUser(specialPointsRows, activeWindowsByUser);

        var rankings = users
            .Select(u =>
            {
                var matchPoints = matchByUser.GetValueOrDefault(u.UserId, 0);
                var specialPoints = specialByUser.GetValueOrDefault(u.UserId, 0);
                return new MonthlyPredictionLeaderboardUserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    AvatarUrl = u.AvatarUrl,
                    MatchPredictionPoints = matchPoints,
                    SpecialPredictionPoints = specialPoints,
                    TotalPoints = matchPoints + specialPoints
                };
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.SpecialPredictionPoints)
            .ThenByDescending(x => x.MatchPredictionPoints)
            .ThenBy(x => x.FullName)
            .ToList();


        for (var i = 0; i < rankings.Count; i++)
            rankings[i].Rank = i + 1;

        return new MonthlyPredictionLeaderboardDto
        {
            Year = targetYear,
            Month = targetMonth,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            Rankings = rankings
        };
    }

    public async Task<MonthlyLeaderboardRewardResultDto> RewardTopUsersForPreviousMonthAsync(CancellationToken ct = default)
    {
        var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var previousMonth = new DateTime(vnNow.Year, vnNow.Month, 1).AddMonths(-1);
        var rewardYear = previousMonth.Year;
        var rewardMonth = previousMonth.Month;
        var rewardMonthKey = $"{rewardYear:D4}-{rewardMonth:D2}";

        // Nếu đã từng trao top1 tháng này thì coi như tháng đó đã reward xong.
        var top1RewardName = BuildRewardName(1, rewardMonthKey);
        var alreadyRewarded = await _db.Rewards
            .AsNoTracking()
            .AnyAsync(r => r.RewardName == top1RewardName && r.UserRewards.Any(), ct);

        if (alreadyRewarded)
        {
            return new MonthlyLeaderboardRewardResultDto
            {
                Year = rewardYear,
                Month = rewardMonth,
                RewardedUsers = 0,
                SkippedBecauseAlreadyRewarded = true
            };
        }

        var leaderboard = await GetMonthlyLeaderboardAsync(rewardYear, rewardMonth, ct);
        var topUsers = leaderboard.Rankings
            .Where(r => r.TotalPoints > 0 && r.Rank <= 3)
            .ToList();

        if (topUsers.Count == 0)
        {
            return new MonthlyLeaderboardRewardResultDto
            {
                Year = rewardYear,
                Month = rewardMonth,
                RewardedUsers = 0,
                SkippedBecauseAlreadyRewarded = false
            };
        }

        var rewardPointsByRank = new Dictionary<int, int>
        {
            [1] = 200,
            [2] = 150,
            [3] = 100
        };

        var nextUserRewardId = await _db.UserRewards.AnyAsync(ct)
            ? await _db.UserRewards.MaxAsync(x => x.UserRewardId, ct) + 1
            : 1;

        var rewardedUsers = 0;

        foreach (var user in topUsers)
        {
            var rewardName = BuildRewardName(user.Rank, rewardMonthKey);
            var reward = await _db.Rewards.FirstOrDefaultAsync(r => r.RewardName == rewardName, ct);
            if (reward == null)
            {
                reward = new Reward
                {
                    RewardName = rewardName,
                    Description = $"Thưởng BXH dự đoán tháng {rewardMonthKey} - Top {user.Rank}",
                    RequiredCorrectPredictions = rewardPointsByRank[user.Rank],
                    IconUrl = null,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Rewards.Add(reward);
                await _db.SaveChangesAsync(ct);
            }

            var alreadyGrantedToUser = await _db.UserRewards
                .AnyAsync(x => x.UserId == user.UserId && x.RewardId == reward.RewardId, ct);
            if (alreadyGrantedToUser)
                continue;

            var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == user.UserId, ct);
            if (stats == null)
            {
                stats = new UserPredictionStats
                {
                    UserId = user.UserId,
                    TotalPredictions = 0,
                    CorrectPredictions = 0,
                    ExactScorePredictions = 0,
                    Points = rewardPointsByRank[user.Rank],
                    LastUpdated = DateTime.UtcNow
                };
                _db.UserPredictionStats.Add(stats);
            }
            else
            {
                stats.Points = (stats.Points ?? 0) + rewardPointsByRank[user.Rank];
                stats.LastUpdated = DateTime.UtcNow;
            }

            _db.UserRewards.Add(new UserReward
            {
                UserRewardId = nextUserRewardId++,
                UserId = user.UserId,
                RewardId = reward.RewardId,
                AwardedAt = DateTime.UtcNow
            });

            await _notificationService.MonthlyLeaderboardRewardAsync(
                user.UserId,
                rewardYear,
                rewardMonth,
                user.Rank,
                rewardPointsByRank[user.Rank],
                ct);

            rewardedUsers++;
        }

        var changedEntries = _db.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged);
        if (changedEntries > 0)
            await _db.SaveChangesAsync(ct);

        return new MonthlyLeaderboardRewardResultDto
        {
            Year = rewardYear,
            Month = rewardMonth,
            RewardedUsers = rewardedUsers,
            SkippedBecauseAlreadyRewarded = false
        };
    }

    private static string BuildRewardName(int rank, string monthKey) => $"Monthly Leaderboard {monthKey} Top {rank}";

    private static (int Year, int Month) ResolveYearMonth(int? year, int? month)
    {
        var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var y = year ?? vnNow.Year;
        var m = month ?? vnNow.Month;
        if (m is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Tháng phải trong khoảng 1..12.");
        if (y is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "Năm không hợp lệ.");
        return (y, m);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetUtcMonthRange(int year, int month)
    {
        var vnStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var vnEnd = vnStart.AddMonths(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(vnStart, VietnamTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(vnEnd, VietnamTimeZone);
        return (utcStart, utcEnd);
    }

    private static Dictionary<Guid, List<(DateTime StartUtc, DateTime EndUtc)>> BuildActiveWindowsByUser(
        IEnumerable<PaymentGrant> paidSubscriptions)
    {
        var result = new Dictionary<Guid, List<(DateTime StartUtc, DateTime EndUtc)>>();
        var grouped = paidSubscriptions.GroupBy(x => x.UserId);

        foreach (var group in grouped)
        {
            DateTime? runningEnd = null;
            var windows = new List<(DateTime StartUtc, DateTime EndUtc)>();

            foreach (var payment in group.OrderBy(x => x.PaidAt))
            {
                var paidAt = payment.PaidAt;
                var durationDays = payment.DurationDays;
                var start = runningEnd.HasValue && runningEnd.Value > paidAt ? runningEnd.Value : paidAt;
                var end = start.AddDays(durationDays);
                windows.Add((start, end));
                runningEnd = end;
            }

            if (windows.Count > 0)
                result[group.Key] = windows;
        }

        return result;
    }

    private static Dictionary<Guid, int> AggregatePointsByUser(
        IEnumerable<PointEvent> rows,
        IReadOnlyDictionary<Guid, List<(DateTime StartUtc, DateTime EndUtc)>> windowsByUser)
    {
        var result = new Dictionary<Guid, int>();

        foreach (var row in rows)
        {
            var userId = row.UserId;
            var eventAt = row.EventAt;
            var points = row.Points;

            if (!windowsByUser.TryGetValue(userId, out var windows))
                continue;

            var isActiveAtEventTime = windows.Any(w => eventAt >= w.StartUtc && eventAt < w.EndUtc);
            if (!isActiveAtEventTime)
                continue;

            result[userId] = result.GetValueOrDefault(userId, 0) + points;
        }

        return result;
    }
}
