using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
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

        [HttpGet("leagues")]
        public async Task<IActionResult> GetAllLeagues()
        {
            var data = await _service.GetAllLeaguesAsync();
            return Ok(data.Select(x => new
            {
                x.LeagueId,
                x.ApiLeagueId,
                x.LeagueName,
                x.LeagueType,
                x.LogoUrl
            }));
        }

        [HttpGet("seasons")]
        public async Task<IActionResult> GetAllSeasons()
        {
            var data = await _service.GetAllSeasonsAsync();
            return Ok(data.Select(x => new
            {
                x.SeasonId,
                x.LeagueId,
                x.Year,
                x.ApiSeasonId
            }));
        }

        [HttpGet("teams")]
        public async Task<IActionResult> GetAllTeams()
        {
            var data = await _service.GetAllTeamsAsync();
            return Ok(data.Select(x => new
            {
                x.TeamId,
                x.TeamName,
                x.ClubId,
                x.ApiTeamId,
                x.LogoUrl,
                x.ShortName,
                x.Founded,
                x.National,
                x.StadiumId,
                x.LeagueId
            }));
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetAllMatches([FromQuery] int? tournamentId = null, [FromQuery] int? seasonId = null)
        {
            var data = await _service.GetAllMatchesAsync(tournamentId, seasonId);
            return Ok(data.Select(x => new
            {
                x.MatchId,
                x.ApiFixtureId,
                x.LeagueId,
                x.SeasonId,
                x.MatchDate,
                x.KickOffTime,
                x.Status,
                x.HomeTeamId,
                x.AwayTeamId,
                x.HomeGoals,
                x.AwayGoals,
                x.Venue,
                x.RefereeName,
                x.Attendance,
                x.ApiTimestamp,
                x.Timezone,
                x.PeriodFirstHalf,
                x.PeriodSecondHalf,
                x.Round,
                x.ApiVenueId
            }));
        }

        [HttpGet("match-statistics")]
        public async Task<IActionResult> GetAllMatchStatistics()
        {
            var data = await _service.GetAllMatchStatisticsAsync();
            return Ok(data.Select(x => new
            {
                x.StatId,
                x.MatchId,
                x.TeamId,
                x.Possession,
                x.Shots,
                x.ShotsOnTarget,
                x.Corners,
                x.Fouls,
                x.YellowCards,
                x.RedCards,
                x.Offsides,
                x.ShotsBlocked,
                x.ShotsInsideBox,
                x.ShotsOutsideBox,
                x.PassesAccuracy,
                x.PassesKey,
                x.DribblesAttempted,
                x.DribblesSuccess,
                x.DuelsWon,
                x.DuelsTotal,
                x.TacklesWon,
                x.Saves,
                x.Interceptions,
                x.Clearances,
                x.ExpectedGoals
            }));
        }

        [HttpGet("match-statistics-by-match")]
        public async Task<IActionResult> GetMatchStatisticsByMatchAsync([FromQuery] int apiFixtureId)
        {
            if (apiFixtureId <= 0)
                return BadRequest(new { status = false, message = "Invalid apiFixtureId" });

            var data = await _service.GetMatchStatisticsByMatchAsync(apiFixtureId);

            if (data == null || !data.Any())
                return NotFound(new { status = false, message = "No statistics found for this match", apiFixtureId });

            return Ok(data.Select(x => new
            {
                x.StatId,
                x.MatchId,
                x.TeamId,
                x.Possession,
                x.Shots,
                x.ShotsOnTarget,
                x.Corners,
                x.Fouls,
                x.YellowCards,
                x.RedCards,
                x.Offsides,
                x.ShotsBlocked,
                x.ShotsInsideBox,
                x.ShotsOutsideBox,
                x.PassesAccuracy,
                x.PassesKey,
                x.DribblesAttempted,
                x.DribblesSuccess,
                x.DuelsWon,
                x.DuelsTotal,
                x.TacklesWon,
                x.Saves,
                x.Interceptions,
                x.Clearances,
                x.ExpectedGoals
            }));
        }

        [HttpGet("team-last-matches-db")]
        public async Task<IActionResult> GetTeamLastMatchesFromDb([FromQuery] int apiTeamId, [FromQuery] int count = 5)
        {
            if (apiTeamId <= 0)
                return BadRequest(new { status = false, message = "Invalid apiTeamId" });

            var data = await _service.GetTeamLastMatchesFromDbAsync(apiTeamId, count);
            return Ok(data.Select(x => new
            {
                x.MatchId,
                x.ApiFixtureId,
                x.MatchDate,
                x.Status,
                x.HomeGoals,
                x.AwayGoals,
                x.Round,
                HomeTeam = x.HomeTeam == null ? null : new { x.HomeTeam.TeamId, x.HomeTeam.ApiTeamId, x.HomeTeam.TeamName, x.HomeTeam.LogoUrl },
                AwayTeam = x.AwayTeam == null ? null : new { x.AwayTeam.TeamId, x.AwayTeam.ApiTeamId, x.AwayTeam.TeamName, x.AwayTeam.LogoUrl },
            }));
        }

        [HttpGet("team-players")]
        public async Task<IActionResult> GetAllTeamPlayers([FromQuery] int sofascoreTeamId)
        {
            var data = await _service.GetAllPlayersAsync(sofascoreTeamId);
            return Ok(data.Select(x => new
            {
                x.PlayerId,
                x.ApiPlayerId,
                x.FirstName,
                x.LastName,
                x.FullName,
                x.DateOfBirth,
                x.Age,
                x.Nationality,
                x.BirthPlace,
                x.BirthCountry,
                x.HeightCm,
                x.WeightKg,
                x.PhotoUrl,
                x.IsInjured,
                x.TeamId,
                x.Position,
                x.Number
            }));
        }

        [HttpGet("all-team-players")]
        public async Task<IActionResult> GetAllTeamPlayersByLeagueSeason(
            [FromQuery] int tournamentId,
            [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });

            var data = await _service.GetAllTeamPlayersByLeagueSeasonAsync(tournamentId, seasonId);
            return Ok(data.Select(x => new
            {
                x.PlayerId,
                x.ApiPlayerId,
                x.FirstName,
                x.LastName,
                x.FullName,
                x.DateOfBirth,
                x.Age,
                x.Nationality,
                x.BirthPlace,
                x.BirthCountry,
                x.HeightCm,
                x.WeightKg,
                x.PhotoUrl,
                x.IsInjured,
                x.TeamId,
                x.Position,
                x.Number
            }));
        }

        [HttpGet("player-season-statistics")]
        public async Task<IActionResult> GetAllPlayerSeasonStatistics()
        {
            var data = await _service.GetAllPlayerSeasonStatisticsAsync();
            return Ok(data.Select(x => new
            {
                x.PlayerStatisticsId,
                x.PlayerId,
                x.TeamId,
                x.LeagueId,
                x.SeasonId,
                x.Appearances,
                x.Lineups,
                x.Minutes,
                x.Goals,
                x.Assists,
                x.YellowCards,
                x.RedCards,
                x.Rating,
                x.SubstitutionsIn,
                x.SubstitutionsOut,
                x.ShotsTotal,
                x.ShotsOnTarget,
                x.PassesTotal,
                x.PassesKey,
                x.PassesAccuracy,
                x.DribblesAttempted,
                x.DribblesSuccess,
                x.DribblesSuccessRate,
                x.DuelsWon,
                x.DuelsTotal,
                x.DuelsWonRate,
                x.Tackles,
                x.Interceptions,
                x.FoulsDrawn,
                x.FoulsCommitted,
                x.PenaltiesScored,
                x.PenaltiesMissed
            }));
        }

        [HttpGet("match-events")]
        public async Task<IActionResult> GetAllMatchEvents([FromQuery] int apiFixtureId)
        {
            var data = await _service.GetAllMatchEventsAsync(apiFixtureId);
            return Ok(data.Select(x => new
            {
                x.EventId,
                x.MatchId,
                x.TeamId,
                x.PlayerId,
                x.AssistPlayerId,
                x.EventType,
                x.Detail,
                x.EventTime,
                x.ExtraTime,
                x.Period,
                x.Comments
            }));
        }

        [HttpGet("standings")]
        public async Task<IActionResult> GetAllStandings([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });

            var data = await _service.GetAllStandingsAsync(tournamentId, seasonId);
            return Ok(data.Select(x => new
            {
                x.StandingId,
                x.LeagueId,
                x.SeasonId,
                x.TeamId,
                TeamName = x.Team != null ? x.Team.TeamName : null,
                TeamLogo = x.Team != null ? x.Team.LogoUrl : null,
                ApiTeamId = x.Team != null ? x.Team.ApiTeamId : (int?)null,
                x.Rank,
                x.Played,
                x.Win,
                x.Draw,
                x.Loss,
                x.GoalsFor,
                x.GoalsAgainst,
                x.GoalDifference,
                x.Points,
                x.Form,
                x.Status,
                x.Description,
                x.HomeRecord,
                x.AwayRecord,
                x.ApiLastUpdated
            }));
        }

        [HttpGet("player-match-statistic-by-match")]
        public async Task<IActionResult> GetAllPlayerMatchStatsByMatch(
        [FromQuery] int apiFixtureId)
        {
            if (apiFixtureId <= 0)
                return BadRequest(new { status = false, message = "Invalid apiFixtureId" });

            if (!await _service.MatchExistsByApiFixtureIdAsync(apiFixtureId))
            {
                return NotFound(new
                {
                    status = false,
                    message = "Không có trận với apiFixtureId này trong database. Hãy POST sync-matches (hoặc đồng bộ trận) trước.",
                    apiFixtureId
                });
            }

            var data = await _service.GetAllPlayerMatchStatisticsByApiFixtureIdAsync(apiFixtureId);
            return Ok(data.Select(x => new
            {
                x.PlayerMatchStatId,
                x.MatchId,
                x.PlayerId,
                x.TeamId,
                x.Minutes,
                x.Goals,
                x.Assists,
                x.Shots,
                x.ShotsOnTarget,
                x.Passes,
                x.PassesAccuracy,
                x.PassesKey,
                x.TotalCrosses,
                x.AccurateCrosses,
                x.TotalLongBalls,
                x.AccurateLongBalls,
                x.PassesOwnHalf,
                x.AccuratePassesOwnHalf,
                x.PassesOppositionHalf,
                x.AccuratePassesOppositionHalf,
                x.Tackles,
                x.TacklesWon,
                x.Interceptions,
                x.Clearances,
                x.Blocks,
                x.DribblesAttempted,
                x.DribblesSuccess,
                x.DuelsWon,
                x.DuelsTotal,
                x.AerialDuelsWon,
                x.AerialDuelsLost,
                x.GroundDuelsWon,
                x.GroundDuelsLost,
                x.FoulsCommitted,
                x.FoulsDrawn,
                x.Offsides,
                x.YellowCards,
                x.RedCards,
                x.PenaltiesScored,
                x.PenaltiesMissed,
                x.PenaltiesWon,
                x.PenaltiesCommitted,
                x.Rating,
                x.ExpectedGoals,
                x.ExpectedAssists,
                x.Touches,
                x.PossessionLost,
                x.BallRecoveries,
                x.Dispossessed,
                x.WasFouled,
                x.UnsuccessfulTouch
            }));
        }

        [HttpGet("player-match-stattistic-by-league")]
        public async Task<IActionResult> GetAllPlayerMatchStatsByLeagueSeason(
            [FromQuery] int tournamentId,
            [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });

            var data = await _service.GetAllPlayerMatchStatisticsByLeagueSeasonAsync(tournamentId, seasonId);
            return Ok(data.Select(x => new
            {
                x.PlayerMatchStatId,
                x.MatchId,
                x.PlayerId,
                x.TeamId,
                x.Minutes,
                x.Goals,
                x.Assists,
                x.Shots,
                x.ShotsOnTarget,
                x.Passes,
                x.PassesAccuracy,
                x.PassesKey,
                x.TotalCrosses,
                x.AccurateCrosses,
                x.TotalLongBalls,
                x.AccurateLongBalls,
                x.PassesOwnHalf,
                x.AccuratePassesOwnHalf,
                x.PassesOppositionHalf,
                x.AccuratePassesOppositionHalf,
                x.Tackles,
                x.TacklesWon,
                x.Interceptions,
                x.Clearances,
                x.Blocks,
                x.DribblesAttempted,
                x.DribblesSuccess,
                x.DuelsWon,
                x.DuelsTotal,
                x.AerialDuelsWon,
                x.AerialDuelsLost,
                x.GroundDuelsWon,
                x.GroundDuelsLost,
                x.FoulsCommitted,
                x.FoulsDrawn,
                x.Offsides,
                x.YellowCards,
                x.RedCards,
                x.PenaltiesScored,
                x.PenaltiesMissed,
                x.PenaltiesWon,
                x.PenaltiesCommitted,
                x.Rating,
                x.ExpectedGoals,
                x.ExpectedAssists,
                x.Touches,
                x.PossessionLost,
                x.BallRecoveries,
                x.Dispossessed,
                x.WasFouled,
                x.UnsuccessfulTouch
            }));
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

        [HttpPost("sync-match-statistics-by-league")]
        public async Task<IActionResult> SyncMatchStatisticsByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            var result = await _service.SyncMatchStatisticsByLeagueAndSeasonAsync(tournamentId, seasonId);
            return Ok(result);
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