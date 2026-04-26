namespace VNFootballLeagues.Services.Models.Predictions;

public class MonthlyPredictionLeaderboardDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public IReadOnlyList<MonthlyPredictionLeaderboardUserDto> Rankings { get; set; } = [];
}

public class MonthlyPredictionLeaderboardUserDto
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int MatchPredictionPoints { get; set; }
    public int SpecialPredictionPoints { get; set; }
    public int TotalPoints { get; set; }
}

public class MonthlyLeaderboardRewardResultDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int RewardedUsers { get; set; }
    public bool SkippedBecauseAlreadyRewarded { get; set; }
}
