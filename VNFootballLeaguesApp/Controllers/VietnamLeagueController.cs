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

        // GET endpoints - retrieve synced data
        [HttpGet("leagues")]
        public async Task<IActionResult> GetLeagues()
        {
            var leagues = await _service.GetLeaguesAsync();
            return Ok(new
            {
                success = true,
                count = leagues.Count,
                data = leagues
            });
        }

        [HttpGet("seasons")]
        public async Task<IActionResult> GetSeasons([FromQuery] int? leagueId = null)
        {
            var seasons = await _service.GetSeasonsAsync(leagueId);
            return Ok(new
            {
                success = true,
                count = seasons.Count,
                data = seasons
            });
        }

        [HttpGet("teams")]
        public async Task<IActionResult> GetTeams([FromQuery] int? leagueId = null)
        {
            var teams = await _service.GetTeamsAsync(leagueId);
            return Ok(new
            {
                success = true,
                count = teams.Count,
                data = teams
            });
        }

        [HttpGet("players")]
        public async Task<IActionResult> GetPlayers([FromQuery] int? teamId = null)
        {
            var players = await _service.GetPlayersAsync(teamId);
            return Ok(new
            {
                success = true,
                count = players.Count,
                data = players
            });
        }

        [HttpGet("player-stats")]
        public async Task<IActionResult> GetPlayerStats(
            [FromQuery] int? playerId = null,
            [FromQuery] int? seasonId = null)
        {
            var stats = await _service.GetPlayerStatsAsync(playerId, seasonId);
            return Ok(new
            {
                success = true,
                count = stats.Count,
                data = stats
            });
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches(
            [FromQuery] int? leagueId = null,
            [FromQuery] int? seasonId = null)
        {
            var matches = await _service.GetMatchesAsync(leagueId, seasonId);
            return Ok(new
            {
                success = true,
                count = matches.Count,
                data = matches.Select(m => new
                {
                    m.MatchId,
                    m.ApiFixtureId,
                    HomeTeam = new { m.HomeTeam.TeamId, m.HomeTeam.TeamName, m.HomeTeam.LogoUrl },
                    AwayTeam = new { m.AwayTeam.TeamId, m.AwayTeam.TeamName, m.AwayTeam.LogoUrl },
                    m.HomeGoals,
                    m.AwayGoals,
                    m.MatchDate,
                    m.Round,
                    m.Status,
                    m.Venue
                })
            });
        }

        [HttpGet("standings")]
        public async Task<IActionResult> GetStandings(
            [FromQuery] int? leagueId = null,
            [FromQuery] int? seasonId = null)
        {
            var standings = await _service.GetStandingsAsync(leagueId, seasonId);
            return Ok(new
            {
                success = true,
                count = standings.Count,
                data = standings.Select(s => new
                {
                    s.StandingId,
                    Team = new { s.Team.TeamId, s.Team.TeamName, s.Team.LogoUrl },
                    s.Rank,
                    s.Points,
                    s.Played,
                    s.Win,
                    s.Draw,
                    s.Loss,
                    s.GoalsFor,
                    s.GoalsAgainst,
                    s.GoalDifference,
                    s.Form
                })
            });
        }
    }
}
