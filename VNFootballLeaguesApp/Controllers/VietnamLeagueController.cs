using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;

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
        public async Task<IActionResult> SyncPlayers(int apiLeagueId, int season)
        {
            var players = await _service.SyncPlayersByLeagueAsync(apiLeagueId, season);
            return Ok(players);
        }
    }
}
