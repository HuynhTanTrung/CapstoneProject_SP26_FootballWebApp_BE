namespace VNFootballLeagues.Services.Dtos;

public record ArticleAnalysisResponse(
    bool Success,
    string Analysis,
    string? DetectedLeague,
    string? Warning,
    ArticleEntities? Entities = null,
    int DailyUsed = 0,
    int DailyLimit = 0,
    int CreditsRemaining = 0
);

public record ArticleEntities(
    List<string> Players,
    List<string> Teams
);
