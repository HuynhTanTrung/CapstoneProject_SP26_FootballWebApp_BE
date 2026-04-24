namespace VNFootballLeagues.Services.Dtos;

public record ArticleAnalysisRequest(
    string ArticleUrl,
    string ArticleTitle,
    string ArticleContent
);
