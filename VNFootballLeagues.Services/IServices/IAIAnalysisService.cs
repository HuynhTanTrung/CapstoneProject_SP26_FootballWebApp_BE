using VNFootballLeagues.Services.Dtos;

namespace VNFootballLeagues.Services.IServices;

public interface IAIAnalysisService
{
    Task<AIAnalysisResponse> AnalyzePlayerRatingAsync(int matchId, int playerId, CancellationToken ct = default);
    Task<AIAnalysisResponse> AnalyzeMatchAsync(int matchId, CancellationToken ct = default);
}
