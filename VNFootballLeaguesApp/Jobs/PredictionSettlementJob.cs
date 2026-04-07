using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Jobs;

/// <summary>
/// Chấm điểm dự đoán khi trận đã có kết quả (chạy định kỳ qua Hangfire).
/// </summary>
public class PredictionSettlementJob
{
    private readonly IPredictionService _predictionService;
    private readonly ILogger<PredictionSettlementJob> _logger;

    public PredictionSettlementJob(IPredictionService predictionService, ILogger<PredictionSettlementJob> logger)
    {
        _predictionService = predictionService;
        _logger = logger;
    }

    public async Task SettlePendingPredictionsAsync()
    {
        try
        {
            var n = await _predictionService.SettleAllPendingAsync();
            if (n > 0)
                _logger.LogInformation("[PredictionSettlement] Đã chấm {Count} dự đoán.", n);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PredictionSettlement] Lỗi khi chấm điểm dự đoán.");
        }
    }
}
