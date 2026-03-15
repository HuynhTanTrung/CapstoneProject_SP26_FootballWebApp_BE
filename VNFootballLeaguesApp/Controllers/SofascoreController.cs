using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;

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

    /// <summary>
    /// Retrieves match lineup data from SofaScore
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON response containing lineup information</returns>
    /// <response code="200">Returns the lineup data</response>
    /// <response code="400">If eventId is invalid</response>
    /// <response code="500">If scraping fails</response>
    [HttpGet("lineups")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLineups([FromQuery] int eventId)
    {
        // Validate input
        if (eventId <= 0)
        {
            return BadRequest(new { error = "Invalid eventId. Must be a positive integer." });
        }

        try
        {
            _logger.LogInformation("Fetching lineup data for event {EventId}", eventId);

            // Call the scraper service
            string jsonResponse = await _sofascoreScraperService.GetMatchLineupsAsync(eventId);

            // Return raw JSON response
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch lineup data for event {EventId}", eventId);
            return StatusCode(500, new { error = "Failed to retrieve lineup data", details = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves tournament standings data from SofaScore
    /// </summary>
    /// <param name="tournamentId">The SofaScore tournament ID</param>
    /// <param name="seasonId">The season ID</param>
    /// <returns>JSON response containing standings information</returns>
    /// <response code="200">Returns the standings data</response>
    /// <response code="400">If parameters are invalid</response>
    /// <response code="500">If scraping fails</response>
    [HttpGet("standings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStandings([FromQuery] int tournamentId, [FromQuery] int seasonId)
    {
        // Validate input
        if (tournamentId <= 0 || seasonId <= 0)
        {
            return BadRequest(new { error = "Invalid parameters. tournamentId and seasonId must be positive integers." });
        }

        try
        {
            _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}", 
                tournamentId, seasonId);

            // Call the scraper service
            string jsonResponse = await _sofascoreScraperService.GetTournamentStandingsAsync(tournamentId, seasonId);

            // Return raw JSON response
            return Content(jsonResponse, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch standings for tournament {TournamentId}, season {SeasonId}", 
                tournamentId, seasonId);
            return StatusCode(500, new { error = "Failed to retrieve standings data", details = ex.Message });
        }
    }
}
