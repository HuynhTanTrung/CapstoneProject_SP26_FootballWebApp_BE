using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SofascoreHybridController : ControllerBase
    {
        private readonly ISofascoreHybridService _service;
        private readonly ILogger<SofascoreHybridController> _logger;

        public SofascoreHybridController(
            ISofascoreHybridService service,
            ILogger<SofascoreHybridController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("sync-matches")]
        public async Task<IActionResult> SyncMatches([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.SyncMatchesByRoundAsync(tournamentId, seasonId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing matches");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-match-statistics")]
        public async Task<IActionResult> SyncMatchStatistics([FromQuery] int apiFixtureId)
        {
            try
            {
                if (apiFixtureId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid apiFixtureId" });
                }

                var result = await _service.SyncMatchStatisticsAsync(apiFixtureId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing match statistics");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-teams")]
        public async Task<IActionResult> SyncTeamsFromStandings([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.SyncTeamsFromStandingsAsync(tournamentId, seasonId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams from standings");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }
    }
}