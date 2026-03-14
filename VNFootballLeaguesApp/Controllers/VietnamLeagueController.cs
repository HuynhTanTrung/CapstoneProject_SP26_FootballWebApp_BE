using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FootballController : ControllerBase
    {
        private readonly IFootballApiService _service;

        public FootballController(IFootballApiService service)
        {
            _service = service;
        }

        [HttpPost("sync-leagues")]
        public async Task<IActionResult> SyncLeagues()
        {
            var leagues = await _service.SyncLeaguesAsync();
            return Ok(leagues);
        }

        [HttpPost("sync-seasons")]
        public async Task<IActionResult> SyncSeasons()
        {
            var seasons = await _service.SyncSeasonsAsync();

            return Ok(new
            {
                success = true,
                message = "Seasons synced successfully",
                count = seasons.Count,
                data = seasons
            });
        }

        [HttpPost("sync-teams")]
        public async Task<IActionResult> SyncTeams(
            [FromQuery] int apiLeagueId,
            [FromQuery] int season)
        {
            var teams = await _service.SyncTeamsByLeagueAsync(apiLeagueId, season);

            return Ok(new
            {
                success = true,
                message = "Teams synced successfully",
                count = teams.Count,
                data = teams
            });
        }

        [HttpPost("sync-players")]
        public async Task<IActionResult> SyncPlayers(
        [FromQuery] int apiLeagueId,
        [FromQuery] int season)
        {
            var players = await _service
                .SyncPlayersByLeagueAsync(apiLeagueId, season);

            return Ok(new
            {
                success = true,
                message = "Players synced successfully",
                count = players.Count,
            });
        }

        [HttpPost("sync-player-stats")]
        public async Task<IActionResult> SyncPlayerStats(
        [FromQuery] int apiLeagueId,
        [FromQuery] int season)
        {
            var stats = await _service
                .SyncPlayerSeasonStatisticsAsync(apiLeagueId, season);

            return Ok(new
            {
                success = true,
                message = "Player statistics synced successfully",
                count = stats.Count
            });
        }

        [HttpPost("sync-matches")]
        public async Task<IActionResult> SyncMatches(
        [FromQuery] int apiLeagueId,
        [FromQuery] int season)
        {
            var matches = await _service.SyncMatchesByLeagueAsync(apiLeagueId, season);

            return Ok(new
            {
                success = true,
                message = "Matches synced successfully",
                count = matches.Count,
                data = matches.Select(m => new
                {
                    m.MatchId,
                    m.ApiFixtureId,
                    m.HomeTeamId,
                    m.AwayTeamId,
                    m.HomeGoals,
                    m.AwayGoals,
                    m.MatchDate,
                    m.Round,
                    m.Status
                })
            });
        }

        [HttpPost("sync-standings")]
        public async Task<IActionResult> SyncStandings(
        [FromQuery] int apiLeagueId,
        [FromQuery] int season)
        {
            var standings = await _service
                .SyncStandingsAsync(apiLeagueId, season);

            return Ok(new
            {
                success = true,
                message = "Standings synced successfully",
                count = standings.Count
            });
        }

        [HttpPost("sync-match-events")]
        public async Task<IActionResult> SyncMatchEvents([FromQuery] int apiFixtureId)
        {
            var events = await _service.SyncMatchEventsAsync(apiFixtureId);

            return Ok(new
            {
                success = true,
                message = "Match events synced successfully",
                count = events.Count,
            });
        }

        [HttpPost("sync-transfers")]
        public async Task<IActionResult> SyncTransfers(
        [FromQuery] int apiTeamId)
        {
            var transfers = await _service.SyncTransfersAsync(apiTeamId);

            return Ok(new
            {
                success = true,
                message = "Transfers synced successfully",
                count = transfers.Count
            });
        }
        //[HttpPost("sync-lineups")]
        //public async Task<IActionResult> SyncLineups([FromQuery] int apiFixtureId)
        //{
        //    var lineups = await _service.SyncLineupsAsync(apiFixtureId);

        //    return Ok(new
        //    {
        //        success = true,
        //        message = "Lineups synced successfully",
        //        count = lineups.Count
        //    });
        //}
    }
}
