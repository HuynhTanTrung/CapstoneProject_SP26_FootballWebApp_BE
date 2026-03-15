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
}
