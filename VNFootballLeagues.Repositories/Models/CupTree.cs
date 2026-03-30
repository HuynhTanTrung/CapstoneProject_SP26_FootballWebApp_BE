namespace VNFootballLeagues.Repositories.Models;

public partial class CupTree
{
    public int CupTreeId { get; set; }
    public int TournamentId { get; set; }
    public int SeasonId { get; set; }
    public string Data { get; set; } // JSON blob from Sofascore
    public DateTime LastUpdated { get; set; }
}
