namespace VNFootballLeagues.Services.Models.Predictions;

public class SubmitPredictionRequest
{
    public int MatchId { get; set; }

    public int PredictedHomeGoals { get; set; }

    public int PredictedAwayGoals { get; set; }
}

public class PredictionSubmitResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public PredictionItemDto? Prediction { get; set; }
}

public class PredictionItemDto
{
    public int PredictionId { get; set; }

    public int MatchId { get; set; }

    public string? HomeTeamName { get; set; }

    public string? AwayTeamName { get; set; }

    public int? PredictedHomeGoals { get; set; }

    public int? PredictedAwayGoals { get; set; }

    public int? ActualHomeGoals { get; set; }

    public int? ActualAwayGoals { get; set; }

    public string? MatchStatus { get; set; }

    /// <summary>0 = chưa chấm hoặc sai, 1 = đúng kết quả, 2 = đúng tỉ số.</summary>
    public int? IsCorrect { get; set; }

    public int? Points { get; set; }

    public DateTime? CreatedAt { get; set; }
}

public class UserPredictionStatsDto
{
    public int TotalPredictions { get; set; }

    public int CorrectPredictions { get; set; }

    public int ExactScorePredictions { get; set; }

    public int Points { get; set; }

    public DateTime? LastUpdated { get; set; }
}

public class RewardDto
{
    public int RewardId { get; set; }

    public string? RewardName { get; set; }

    public string? Description { get; set; }

    /// <summary>Với huy hiệu dự đoán: ngưỡng tổng điểm tối thiểu (cột DB: RequiredCorrectPredictions).</summary>
    public int? RequiredCorrectPredictions { get; set; }

    public string? IconUrl { get; set; }
}

public class UserRewardDto
{
    public int UserRewardId { get; set; }

    public DateTime? AwardedAt { get; set; }

    public int? RewardId { get; set; }

    public string? RewardName { get; set; }

    public string? Description { get; set; }

    /// <summary>URL ảnh huy hiệu (static hoặc CDN).</summary>
    public string? IconUrl { get; set; }
}
