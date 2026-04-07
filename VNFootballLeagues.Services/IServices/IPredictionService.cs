using VNFootballLeagues.Services.Models.Predictions;

namespace VNFootballLeagues.Services.IServices;

public interface IPredictionService
{
    Task<PredictionSubmitResult> SubmitPredictionAsync(Guid userId, SubmitPredictionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PredictionItemDto>> GetMyPredictionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PredictionItemDto?> GetMyPredictionForMatchAsync(Guid userId, int matchId, CancellationToken cancellationToken = default);

    Task<UserPredictionStatsDto?> GetMyStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RewardDto>> GetRewardsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRewardDto>> GetMyRewardsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Chấm điểm mọi dự đoán chưa chấm của trận (sau khi có tỉ số chính thức).</summary>
    Task<int> SettleMatchAsync(int matchId, CancellationToken cancellationToken = default);

    /// <summary>Quét các trận đã có kết quả nhưng dự đoán chưa được chấm.</summary>
    Task<int> SettleAllPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Tính lại UserPredictionStats từ bảng Prediction và trao huy hiệu (dùng khi test / sửa dữ liệu tay).</summary>
    Task RecomputeUserStatsAndBadgesAsync(Guid userId, CancellationToken cancellationToken = default);
}
