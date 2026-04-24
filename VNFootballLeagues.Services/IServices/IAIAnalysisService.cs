using VNFootballLeagues.Services.Dtos;

namespace VNFootballLeagues.Services.IServices;

public interface IAIAnalysisService
{
    Task<AIAnalysisResponse> AnalyzePlayerRatingAsync(int matchId, int playerId, Guid userId, CancellationToken ct = default);
    Task<AIAnalysisResponse> AnalyzeMatchAsync(int matchId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AIAnalysisHistoryDto>> GetUserHistoryAsync(Guid userId, int page = 1, int pageSize = 20, string[]? types = null);
}
