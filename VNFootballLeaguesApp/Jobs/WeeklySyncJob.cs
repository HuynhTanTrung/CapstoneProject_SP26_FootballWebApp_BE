using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeaguesApp.Jobs;

/// <summary>
/// Weekly sync job — runs every Monday at 3:00 AM (UTC+7)
/// Syncs matches, standings, lineups, match stats, and player stats
/// for all 3 Vietnamese leagues.
/// Player match stats are synced for all FT matches in the past 7 days
/// to handle postponed matches from previous rounds.
/// </summary>
public class WeeklySyncJob
{
    private readonly ISofascoreHybridService _service;
    private readonly VNFootballLeaguesDBContext _context;
    private readonly ILogger<WeeklySyncJob> _logger;

    // V-League 1, V-League 2, Vietnam Cup
    private static readonly (int TournamentId, int SeasonId)[] Leagues =
    [
        (626, 78589),
        (771, 80926),
        (3087, 81023),
    ];

    public WeeklySyncJob(
        ISofascoreHybridService service,
        VNFootballLeaguesDBContext context,
        ILogger<WeeklySyncJob> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

    public async Task SyncMatchesAndStandingsAsync()
    {
        _logger.LogInformation("[WeeklySync] Starting matches + standings sync");
        foreach (var (tid, sid) in Leagues)
        {
            try
            {
                await _service.SyncMatchesByRoundAsync(tid, sid);
                _logger.LogInformation("[WeeklySync] Synced matches for tournament {T} season {S}", tid, sid);
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed matches for {T}/{S}", tid, sid); }

            try
            {
                await _service.SyncStandingsAsync(tid, sid);
                _logger.LogInformation("[WeeklySync] Synced standings for tournament {T} season {S}", tid, sid);
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed standings for {T}/{S}", tid, sid); }
        }
    }

    public async Task SyncLineupsAndMatchStatsAsync()
    {
        _logger.LogInformation("[WeeklySync] Starting lineups + match statistics sync");
        foreach (var (tid, sid) in Leagues)
        {
            try
            {
                await _service.FetchLineupsByLeagueSeasonAsync(tid, sid);
                _logger.LogInformation("[WeeklySync] Synced lineups for tournament {T} season {S}", tid, sid);
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed lineups for {T}/{S}", tid, sid); }

            try
            {
                await _service.SyncMatchStatisticsByLeagueAndSeasonAsync(tid, sid);
                _logger.LogInformation("[WeeklySync] Synced match stats for tournament {T} season {S}", tid, sid);
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed match stats for {T}/{S}", tid, sid); }
        }
    }

    /// <summary>
    /// Sync player match stats for all FT matches played in the past 7 days.
    /// This catches both current-round matches and postponed matches from earlier rounds.
    /// </summary>
    public async Task SyncPlayerMatchStatsForRecentMatchesAsync()
    {
        _logger.LogInformation("[WeeklySync] Starting player match stats sync for recent FT matches");

        var since = DateTime.UtcNow.AddDays(-7);
        var recentMatches = await _context.Matches
            .Where(m => m.ApiFixtureId != null
                && (m.Status == "FT" || m.Status == "finished")
                && m.MatchDate >= since)
            .Select(m => m.ApiFixtureId!.Value)
            .ToListAsync();

        _logger.LogInformation("[WeeklySync] Found {Count} FT matches in past 7 days", recentMatches.Count);

        foreach (var fixtureId in recentMatches)
        {
            try
            {
                await _service.FetchPlayerMatchStatsByApiMatchIdAsync(fixtureId);
                _logger.LogInformation("[WeeklySync] Synced player stats for fixture {Id}", fixtureId);
                await Task.Delay(300); // avoid rate limiting
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed player stats for fixture {Id}", fixtureId); }
        }
    }

    public async Task SyncPlayerSeasonStatsAsync()
    {
        _logger.LogInformation("[WeeklySync] Starting player season statistics sync");
        foreach (var (tid, sid) in Leagues)
        {
            try
            {
                await _service.SyncAllPlayerStatisticsAsync(tid, sid);
                _logger.LogInformation("[WeeklySync] Synced player season stats for tournament {T} season {S}", tid, sid);
            }
            catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed player season stats for {T}/{S}", tid, sid); }
        }
    }

    public async Task SyncCupTreeAsync()
    {
        _logger.LogInformation("[WeeklySync] Starting cup tree sync");
        try
        {
            // Vietnam Cup
            await _service.SyncMatchesByRoundAsync(3087, 81023);
            _logger.LogInformation("[WeeklySync] Synced cup tree");
        }
        catch (Exception ex) { _logger.LogError(ex, "[WeeklySync] Failed cup tree sync"); }
    }
}
