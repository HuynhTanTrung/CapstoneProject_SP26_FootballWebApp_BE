namespace VNFootballLeagues.Services.Dtos;

public record ArticleAnalysisResponse(
    bool Success,
    string Analysis,
    string? DetectedLeague,
    string? Warning,
    ArticleEntities? Entities = null
);

public record ArticleEntities(
    List<string> Players,
    List<string> Teams
);
