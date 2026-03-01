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

        [HttpGet("vietnam-teams")]
        public async Task<IActionResult> GetVietnamTeams(int season)
        {
            var data = await _service.GetVietnamTeamsAsync(season);
            return Ok(data);
        }

        [HttpPost("vietnam-players-by-team")]
        public async Task<IActionResult> SyncPlayersByTeam(
            [FromQuery] int teamApiId,
            [FromQuery] int season)
        {
            var players = await _service.SyncPlayersByTeamAsync(teamApiId, season);

            return Ok(new
            {
                success = true,
                message = "Team players synced successfully.",
                count = players.Count,
                data = players
            });
        }

        [HttpPost("vietnam-players-by-league")]
        public async Task<IActionResult> SyncPlayersByLeague(
            [FromQuery] int season)
        {
            var players = await _service.SyncPlayersByLeagueAsync(season);

            return Ok(new
            {
                success = true,
                message = "League players synced successfully",
                count = players.Count,
                data = players
            });
        }
    }
}
