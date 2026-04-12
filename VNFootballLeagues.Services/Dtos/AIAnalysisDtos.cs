namespace VNFootballLeagues.Services.Dtos;

public record PlayerRatingAnalysisRequest(int MatchId, int PlayerId);

public record MatchAnalysisRequest(int MatchId);

public record AIAnalysisResponse(
    bool Success,
    string Mode,
    string AnalysisVi,
    object Context,
    string? Warning);
