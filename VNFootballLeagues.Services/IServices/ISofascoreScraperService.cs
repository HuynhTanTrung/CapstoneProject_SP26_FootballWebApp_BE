namespace VNFootballLeagues.Services.IServices;

/// <summary>
/// Service interface for scraping SofaScore data using browser automation
/// </summary>
public interface ISofascoreScraperService
{
    /// <summary>
    /// Fetches match lineup data from SofaScore API using PuppeteerSharp
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON string containing lineup data</returns>
    Task<string> GetMatchLineupsAsync(int eventId);

    /// <summary>
    /// Fetches tournament standings data from SofaScore API
    /// </summary>
    /// <param name="tournamentId">The SofaScore tournament ID</param>
    /// <param name="seasonId">The season ID</param>
    /// <returns>JSON string containing standings data</returns>
    Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId);
}
