using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Models;

namespace VNFootballLeagues.Services.Services;

public class CheckInService
{
    private const int STREAK_BONUS_THRESHOLD = 5;
    private const int BASE_POINTS = 1;
    private const int BONUS_POINTS = 2;

    private readonly VNFootballLeaguesDBContext _db;

    public CheckInService(VNFootballLeaguesDBContext db) => _db = db;

    private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private static DateTime TodayVN() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz).Date;

    public async Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
    {
        var today = TodayVN();

        var existing = await _db.DailyCheckIns
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CheckInDate == today, ct);

        if (existing != null)
            return new CheckInResultDto
            {
                AlreadyCheckedIn = true,
                PointsEarned = 0,
                CurrentStreak = existing.Streak,
                TotalCheckInPoints = await GetTotalCheckInPointsAsync(userId, ct)
            };

        var yesterday = await _db.DailyCheckIns
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CheckInDate == today.AddDays(-1), ct);

        int streak = yesterday != null ? yesterday.Streak + 1 : 1;
        int points = streak > STREAK_BONUS_THRESHOLD ? BONUS_POINTS : BASE_POINTS;

        _db.DailyCheckIns.Add(new DailyCheckIn
        {
            UserId = userId,
            CheckInDate = today,
            Streak = streak,
            PointsEarned = points
        });

        var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (stats == null)
        {
            _db.UserPredictionStats.Add(new UserPredictionStats
            {
                UserId = userId,
                Points = points,
                TotalPredictions = 0,
                CorrectPredictions = 0,
                LastUpdated = DateTime.UtcNow
            });
        }
        else
        {
            stats.Points = (stats.Points ?? 0) + points;
            stats.LastUpdated = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // Notify streak milestones
        if (streak == 7 || streak == 30 || streak == 100)
        {
            var notifSvc = new NotificationService(_db);
            _ = Task.Run(() => notifSvc.CheckinStreakAsync(userId, streak));
        }

        return new CheckInResultDto
        {
            AlreadyCheckedIn = false,
            PointsEarned = points,
            CurrentStreak = streak,
            TotalCheckInPoints = await GetTotalCheckInPointsAsync(userId, ct)
        };
    }

    public async Task<CheckInStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var today = TodayVN();
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        var thisMonthCheckIns = await _db.DailyCheckIns
            .Where(c => c.UserId == userId && c.CheckInDate >= firstOfMonth && c.CheckInDate <= today)
            .OrderByDescending(c => c.CheckInDate)
            .ToListAsync(ct);

        var todayEntry = thisMonthCheckIns.FirstOrDefault(c => c.CheckInDate == today);
        int currentStreak = todayEntry?.Streak ?? 0;

        if (todayEntry == null)
        {
            var yesterdayEntry = await _db.DailyCheckIns
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CheckInDate == today.AddDays(-1), ct);
            currentStreak = yesterdayEntry?.Streak ?? 0;
        }

        return new CheckInStatusDto
        {
            CheckedInToday = todayEntry != null,
            CurrentStreak = currentStreak,
            TotalCheckInPoints = await GetTotalCheckInPointsAsync(userId, ct),
            CheckedDatesThisMonth = thisMonthCheckIns.Select(c => c.CheckInDate.ToString("yyyy-MM-dd")).ToList()
        };
    }

    private async Task<int> GetTotalCheckInPointsAsync(Guid userId, CancellationToken ct) =>
        await _db.DailyCheckIns.Where(c => c.UserId == userId).SumAsync(c => c.PointsEarned, ct);
}
