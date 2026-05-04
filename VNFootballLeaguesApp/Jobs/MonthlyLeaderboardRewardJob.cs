using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Jobs;

public class MonthlyLeaderboardRewardJob
{
    private readonly IMonthlyPredictionLeaderboardService _service;
    private readonly ILogger<MonthlyLeaderboardRewardJob> _logger;

    public MonthlyLeaderboardRewardJob(
        IMonthlyPredictionLeaderboardService service,
        ILogger<MonthlyLeaderboardRewardJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task RewardPreviousMonthTopUsersAsync()
    {
        try
        {
            var result = await _service.RewardTopUsersForPreviousMonthAsync();
            if (result.SkippedBecauseAlreadyRewarded)
            {
                _logger.LogInformation(
                    "[MonthlyLeaderboardReward] Bỏ qua vì tháng {Month}/{Year} đã trao thưởng.",
                    result.Month,
                    result.Year);
                return;
            }

            _logger.LogInformation(
                "[MonthlyLeaderboardReward] Đã trao thưởng tháng {Month}/{Year} cho {Count} user.",
                result.Month,
                result.Year,
                result.RewardedUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MonthlyLeaderboardReward] Lỗi khi trao thưởng top tháng.");
            throw;
        }
    }
}
