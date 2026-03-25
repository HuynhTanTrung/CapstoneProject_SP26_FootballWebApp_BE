using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.Services;
using System.Text.Json;

namespace VNFootballLeaguesApp.Controllers;

/// <summary>
/// Controller for SofaScore data scraping endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SofascoreController : ControllerBase
{
    private readonly ISofascoreScraperService _sofascoreScraperService;
    private readonly ILogger<SofascoreController> _logger;

    public SofascoreController(
        ISofascoreScraperService sofascoreScraperService,
        ILogger<SofascoreController> logger)
    {
        _sofascoreScraperService = sofascoreScraperService;
        _logger = logger;
    }

    // ==================== VIETNAMESE LEAGUES INFO ====================

    /// <summary>
    /// Get Vietnamese leagues info (uniqueTournamentId and current seasonId)
    /// </summary>
    [HttpGet("vietnamese-leagues")]
    public IActionResult GetVietnameseLeagues()
    {
        var leagues = new[]
        {
            new
            {
                name = "V-League 1",
                uniqueTournamentId = 626,
                currentSeasonId = 78589,
                seasonName = "25/26",
                url = "https://www.sofascore.com/football/tournament/vietnam/v-league-1/626"
            },
            new
            {
                name = "V-League 2",
                uniqueTournamentId = 771,
                currentSeasonId = 80926,
                seasonName = "25/26",
                url = "https://www.sofascore.com/football/tournament/vietnam/v-league-2/771"
            },
            new
            {
                name = "Vietnam Cup",
                uniqueTournamentId = 3087,
                currentSeasonId = 81023,
                seasonName = "25/26",
                url = "https://www.sofascore.com/football/tournament/vietnam/vietnam-cup/3087"
            }
        };

        return Ok(new
        {
            leagues,
            note = "Use these IDs for tournament endpoints (last-matches, next-matches, round-matches, standings)"
        });
    }

    // ==================== LIVE MATCHES ====================

    /// <summary>
    /// Get live matches from Vietnamese leagues only (V-League 1, V-League 2, Vietnam Cup)
    /// </summary>
    [HttpGet("live-matches")]
    public async Task<IActionResult> GetLiveMatches()
    {
        try
        {
            string jsonResponse = await _sofascoreScraperService.GetVietnameseLeagueLiveMatchesAsync();
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Vietnamese league live matches");
            return StatusCode(500, new { error = "Failed to retrieve live matches", details = ex.Message });
        }
    }

    // ==================== TOURNAMENT MATCHES ====================

    /// <summary>
    /// Get last (previous) matches for a tournament
    /// </summary>
    [HttpGet("tournament/last-matches")]
    public async Task<IActionResult> GetTournamentLastMatches(
        [FromQuery] int uniqueTournamentId, 
        [FromQuery] int seasonId, 
        [FromQuery] int page = 0)
    {
        if (uniqueTournamentId <= 0 || seasonId <= 0)
        {
            return BadRequest(new { error = "Invalid parameters" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTournamentLastMatchesAsync(uniqueTournamentId, seasonId, page);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch last matches");
            return StatusCode(500, new { error = "Failed to retrieve last matches", details = ex.Message });
        }
    }

    /// <summary>
    /// Get next (upcoming) matches for a tournament
    /// </summary>
    [HttpGet("tournament/next-matches")]
    public async Task<IActionResult> GetTournamentNextMatches(
        [FromQuery] int uniqueTournamentId, 
        [FromQuery] int seasonId, 
        [FromQuery] int page = 0)
    {
        if (uniqueTournamentId <= 0 || seasonId <= 0)
        {
            return BadRequest(new { error = "Invalid parameters" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTournamentNextMatchesAsync(uniqueTournamentId, seasonId, page);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch next matches");
            return StatusCode(500, new { error = "Failed to retrieve next matches", details = ex.Message });
        }
    }

    /// <summary>
    /// Get matches for a specific round in a tournament
    /// </summary>
    [HttpGet("tournament/round-matches")]
    public async Task<IActionResult> GetTournamentRoundMatches(
        [FromQuery] int uniqueTournamentId, 
        [FromQuery] int seasonId, 
        [FromQuery] int round)
    {
        if (uniqueTournamentId <= 0 || seasonId <= 0 || round <= 0)
        {
            return BadRequest(new { error = "Invalid parameters" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTournamentRoundMatchesAsync(uniqueTournamentId, seasonId, round);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch round matches");
            return StatusCode(500, new { error = "Failed to retrieve round matches", details = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves tournament standings
    /// </summary>
    [HttpGet("standings")]
    public async Task<IActionResult> GetStandings([FromQuery] int tournamentId, [FromQuery] int seasonId)
    {
        if (tournamentId <= 0 || seasonId <= 0)
        {
            return BadRequest(new { error = "Invalid parameters" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTournamentStandingsAsync(tournamentId, seasonId);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch standings");
            return StatusCode(500, new { error = "Failed to retrieve standings", details = ex.Message });
        }
    }

    // ==================== TEAM MATCHES ====================

    /// <summary>
    /// Get last (previous) matches for a team
    /// </summary>
    [HttpGet("team/last-matches")]
    public async Task<IActionResult> GetTeamLastMatches([FromQuery] int teamId, [FromQuery] int page = 0)
    {
        if (teamId <= 0)
        {
            return BadRequest(new { error = "Invalid teamId" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTeamLastMatchesAsync(teamId, page);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch team last matches");
            return StatusCode(500, new { error = "Failed to retrieve team last matches", details = ex.Message });
        }
    }

    /// <summary>
    /// Get next (upcoming) matches for a team
    /// </summary>
    [HttpGet("team/next-matches")]
    public async Task<IActionResult> GetTeamNextMatches([FromQuery] int teamId, [FromQuery] int page = 0)
    {
        if (teamId <= 0)
        {
            return BadRequest(new { error = "Invalid teamId" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetTeamNextMatchesAsync(teamId, page);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch team next matches");
            return StatusCode(500, new { error = "Failed to retrieve team next matches", details = ex.Message });
        }
    }

    // ==================== MATCH DETAILS ====================

    /// <summary>
    /// Retrieves match lineup data from SofaScore
    /// </summary>
    [HttpGet("lineups")]
    public async Task<IActionResult> GetLineups([FromQuery] int eventId)
    {
        if (eventId <= 0)
        {
            return BadRequest(new { error = "Invalid eventId" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetMatchLineupsAsync(eventId);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch lineup for event {EventId}", eventId);
            return StatusCode(500, new { error = "Failed to retrieve lineup", details = ex.Message });
        }
    }

    /// <summary>
    /// Get raw incidents data for debugging
    /// </summary>
    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidents([FromQuery] int eventId)
    {
        if (eventId <= 0)
        {
            return BadRequest(new { error = "Invalid eventId" });
        }

        try
        {
            string jsonResponse = await _sofascoreScraperService.GetMatchIncidentsAsync(eventId);
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch incidents for event {EventId}", eventId);
            return StatusCode(500, new { error = "Failed to retrieve incidents", details = ex.Message });
        }
    }

    // ==================== REALTIME TRACKING ====================

    /// <summary>
    /// Add match to realtime tracking (poll incidents every 60s)
    /// Optionally provide match info for better updates
    /// </summary>
    [HttpPost("tracking/add")]
    public IActionResult AddMatchTracking(
        [FromQuery] int eventId,
        [FromQuery] string? homeTeam = null,
        [FromQuery] string? awayTeam = null,
        [FromQuery] string? status = null)
    {
        if (eventId <= 0)
        {
            return BadRequest(new { error = "Invalid eventId" });
        }

        if (!string.IsNullOrEmpty(homeTeam) && !string.IsNullOrEmpty(awayTeam))
        {
            LiveMatchPollingService.AddMatchWithInfo(eventId, homeTeam, awayTeam, status ?? "live");
        }
        else
        {
            LiveMatchPollingService.AddMatch(eventId);
        }

        _logger.LogInformation("Added match {EventId} to tracking", eventId);

        return Ok(new 
        { 
            message = $"Match {eventId} added to tracking",
            trackedMatches = LiveMatchPollingService.GetTrackedMatches()
        });
    }

    /// <summary>
    /// Remove match from realtime tracking
    /// </summary>
    [HttpDelete("tracking/remove")]
    public IActionResult RemoveMatchTracking([FromQuery] int eventId)
    {
        if (eventId <= 0)
        {
            return BadRequest(new { error = "Invalid eventId" });
        }

        LiveMatchPollingService.RemoveMatch(eventId);
        _logger.LogInformation("Removed match {EventId} from tracking", eventId);

        return Ok(new 
        { 
            message = $"Match {eventId} removed from tracking",
            trackedMatches = LiveMatchPollingService.GetTrackedMatches()
        });
    }

    /// <summary>
    /// Get list of currently tracked matches
    /// </summary>
    [HttpGet("tracking/list")]
    public IActionResult GetTrackedMatches()
    {
        var trackedMatches = LiveMatchPollingService.GetTrackedMatches();
        return Ok(new 
        { 
            count = trackedMatches.Count,
            matches = trackedMatches
        });
    }

    /// <summary>
    /// Clear all tracked matches
    /// </summary>
    [HttpDelete("tracking/clear")]
    public IActionResult ClearTracking()
    {
        LiveMatchPollingService.ClearAllMatches();
        _logger.LogInformation("Cleared all tracked matches");
        return Ok(new { message = "All tracked matches cleared" });
    }
}
