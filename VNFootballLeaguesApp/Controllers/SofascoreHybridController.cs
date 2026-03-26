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

        [HttpPost("sync-vietnamese-leagues")]
        public async Task<IActionResult> SyncVietnameseLeagues()
        {
            var result = await _service.SyncVietnameseLeaguesAsync();

            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("sync-seasons")]
        public async Task<IActionResult> SyncSeasonsByLeague(int apiTournamentId)
        {
            var result = await _service.SyncSeasonsByLeagueAsync(apiTournamentId);

            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
            {
                return Ok(result);
            }

            return BadRequest(result);
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

        
        [HttpPost("sync-team-players")]
        public async Task<IActionResult> SyncTeamPlayers([FromQuery] int sofascoreTeamId)
        {
            if (sofascoreTeamId <= 0)
                return BadRequest(new { status = false, message = "Invalid sofascoreTeamId" });

            var result = await _service.SyncTeamPlayersAsync(sofascoreTeamId);
            return Ok(result);
        }

        [HttpPost("sync-all-team-players")]
        public async Task<IActionResult> SyncAllTeamPlayers([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });

            var result = await _service.SyncAllTeamPlayersAsync(tournamentId, seasonId);
            return Ok(result);
        }

        [HttpPost("sync-player-season-statistics")]
        public async Task<IActionResult> SyncPlayerStatistics([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { status = false, message = "Invalid parameters" });

            var result = await _service.SyncAllPlayerStatisticsAsync(tournamentId, seasonId);
            return Ok(result);
        }

        [HttpPost("sync-match-events")]
        public async Task<IActionResult> SyncMatchEvents([FromQuery] int apiFixtureId)
        {
            try
            {
                if (apiFixtureId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid apiFixtureId" });
                }

                var result = await _service.SyncMatchEventsAsync(apiFixtureId);

                if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing match events for fixture {ApiFixtureId}", apiFixtureId);
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-standings")]
        public async Task<IActionResult> SyncStandings([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.SyncStandingsAsync(tournamentId, seasonId);

                if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing standings for tournament {TournamentId}, season {SeasonId}",
                    tournamentId, seasonId);
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("fetch-player-match-stats-by-match")]
        public async Task<IActionResult> FetchPlayerMatchStatsByApiMatchId([FromQuery] int apiFixtureId)
        {
            var result = await _service.FetchPlayerMatchStatsByApiMatchIdAsync(apiFixtureId);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("fetch-player-match-stats-by-league-season")]
        public async Task<IActionResult> FetchPlayerMatchStatsByLeagueSeason(
            [FromQuery] int tournamentId,
            [FromQuery] int seasonId)
        {
            var result = await _service.FetchPlayerMatchStatsByLeagueSeasonAsync(tournamentId, seasonId);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                return Ok(result);
            return BadRequest(result);
        }
    }
}