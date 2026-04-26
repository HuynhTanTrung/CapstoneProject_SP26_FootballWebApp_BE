using VNFootballLeagues.Services.Models.Predictions;

namespace VNFootballLeagues.Services.IServices;

public interface IMonthlyPredictionLeaderboardService
{
    Task<MonthlyPredictionLeaderboardDto> GetMonthlyLeaderboardAsync(int? year = null, int? month = null, CancellationToken ct = default);
    Task<MonthlyLeaderboardRewardResultDto> RewardTopUsersForPreviousMonthAsync(CancellationToken ct = default);
}
