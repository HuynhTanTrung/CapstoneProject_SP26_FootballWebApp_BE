namespace VNFootballLeagues.Services.Models;

/// <summary>
/// Model for live match updates to be sent via SignalR
/// </summary>
public class LiveMatchUpdate
{
    public int EventId { get; set; }
    public string? HomeTeam { get; set; }
    public string? AwayTeam { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? Status { get; set; }
    public int? CurrentMinute { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<IncidentUpdate>? RecentIncidents { get; set; }
}

/// <summary>
/// Model for match incidents (goals, cards, substitutions)
/// </summary>
public class IncidentUpdate
{
    public string? Type { get; set; } // goal, card, substitution, period, injuryTime
    public int? Time { get; set; }
    public string? Player { get; set; }
    public string? Team { get; set; }
    public string? IncidentClass { get; set; } // yellow, red, etc.
    public int? Length { get; set; } // For injuryTime - number of added minutes
}
