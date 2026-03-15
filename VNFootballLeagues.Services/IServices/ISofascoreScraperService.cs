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

    /// <summary>
    /// Fetches live matches currently in progress
    /// </summary>
    /// <returns>JSON string containing live matches data</returns>
    Task<string> GetLiveMatchesAsync();

    /// <summary>
    /// Fetches live and upcoming matches for Vietnamese leagues only
    /// Filters for V-League 1, V-League 2, and Vietnam Cup
    /// </summary>
    /// <returns>JSON string containing filtered Vietnamese league matches</returns>
    Task<string> GetVietnameseLeagueLiveMatchesAsync();

    /// <summary>
    /// Fetches match incidents (goals, cards, substitutions) for a specific event
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON string containing incidents data</returns>
    Task<string> GetMatchIncidentsAsync(int eventId);

    /// <summary>
    /// Fetches last (previous) matches for a tournament
    /// </summary>
    /// <param name="uniqueTournamentId">The unique tournament ID (626, 771, 3087)</param>
    /// <param name="seasonId">The season ID</param>
    /// <param name="page">Page number (0-based)</param>
    /// <returns>JSON string containing past matches</returns>
    Task<string> GetTournamentLastMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0);

    /// <summary>
    /// Fetches next (upcoming) matches for a tournament
    /// </summary>
    /// <param name="uniqueTournamentId">The unique tournament ID (626, 771, 3087)</param>
    /// <param name="seasonId">The season ID</param>
    /// <param name="page">Page number (0-based)</param>
    /// <returns>JSON string containing upcoming matches</returns>
    Task<string> GetTournamentNextMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0);

    /// <summary>
    /// Fetches matches for a specific round in a tournament
    /// </summary>
    /// <param name="uniqueTournamentId">The unique tournament ID</param>
    /// <param name="seasonId">The season ID</param>
    /// <param name="round">Round number</param>
    /// <returns>JSON string containing round matches</returns>
    Task<string> GetTournamentRoundMatchesAsync(int uniqueTournamentId, int seasonId, int round);

    /// <summary>
    /// Fetches last (previous) matches for a team
    /// </summary>
    /// <param name="teamId">The team ID</param>
    /// <param name="page">Page number (0-based)</param>
    /// <returns>JSON string containing past matches</returns>
    Task<string> GetTeamLastMatchesAsync(int teamId, int page = 0);

    /// <summary>
    /// Fetches next (upcoming) matches for a team
    /// </summary>
    /// <param name="teamId">The team ID</param>
    /// <param name="page">Page number (0-based)</param>
    /// <returns>JSON string containing upcoming matches</returns>
    Task<string> GetTeamNextMatchesAsync(int teamId, int page = 0);
}
