using VNFootballLeagues.Services.Dtos;

namespace VNFootballLeagues.Services.IServices;

public interface IArticleAnalysisService
{
    Task<ArticleAnalysisResponse> AnalyzeArticleAsync(ArticleAnalysisRequest request, Guid userId, CancellationToken ct = default);
}
