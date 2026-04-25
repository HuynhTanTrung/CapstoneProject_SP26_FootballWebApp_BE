namespace VNFootballLeagues.Repositories.Models;

public class MatchCacheData
{
    public int ApiFixtureId { get; set; }
    public string LineupsJson { get; set; }
    public string IncidentsJson { get; set; }
    public DateTime SyncedAt { get; set; }
}
