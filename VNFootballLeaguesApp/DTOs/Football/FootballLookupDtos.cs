namespace VNFootballLeaguesApp.DTOs.Football;

public record RoundDto(string Round, int MatchCount);

public record MatchListItemDto(
    int MatchId,
    DateTime? MatchDate,
    int? HomeTeamId,
    string HomeTeamName,
    int? AwayTeamId,
    string AwayTeamName,
    int? HomeGoals,
    int? AwayGoals,
    string Status,
    string Round,
    int? HomeApiTeamId = null,
    int? AwayApiTeamId = null);

public record PlayerInMatchDto(
    int PlayerId,
    string FullName,
    int? TeamId,
    string TeamName,
    string Position,
    decimal? Rating,
    int? Minutes,
    string? PhotoUrl = null,
    int? ApiPlayerId = null);

public record MatchEventDto(
    int EventId,
    int? MatchId,
    int? TeamId,
    string TeamName,
    int? PlayerId,
    string PlayerName,
    int? AssistPlayerId,
    string AssistPlayerName,
    string EventType,
    string Detail,
    int? EventTime,
    int? ExtraTime,
    string Period,
    string Comments);
