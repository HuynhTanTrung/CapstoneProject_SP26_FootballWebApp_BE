using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SofascoreHybridController : ControllerBase
    {
        private readonly ISofascoreHybridService _service;
        private readonly ISofascoreScraperService _sofascoreScraperService;
        private readonly VNFootballLeaguesDBContext _context;
        private readonly ILogger<SofascoreHybridController> _logger;

        public SofascoreHybridController(
            ISofascoreHybridService service,
            ISofascoreScraperService sofascoreScraperService,
            VNFootballLeaguesDBContext context,
            ILogger<SofascoreHybridController> logger)
        {
            _service = service;
            _sofascoreScraperService = sofascoreScraperService;
            _context = context;
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

        /// <summary>
        /// leagueId = khóa nội bộ (League.LeagueId). tournamentId = Api SofaScore (unique tournament), dùng khi không nhớ leagueId.
        /// Nếu DB chưa có mùa, hệ thống gọi API SofaScore (cần tournamentId hoặc đã sync giải để lọc theo league).
        /// </summary>
        [HttpGet("seasons")]
        public async Task<IActionResult> GetAllSeasons([FromQuery] int? leagueId, [FromQuery] int? tournamentId)
        {
            var data = await _service.GetAllSeasonsAsync(leagueId, tournamentId);
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

        [HttpGet("team-last-matches-db")]
        public async Task<IActionResult> GetTeamLastMatchesFromDb([FromQuery] int apiTeamId, [FromQuery] int count = 5)
        {
            if (apiTeamId <= 0) return BadRequest(new { status = false, message = "Invalid apiTeamId" });
            var data = await _service.GetTeamLastMatchesFromDbAsync(apiTeamId, count);
            return Ok(data.Select(x => new
            {
                x.MatchId,
                x.ApiFixtureId,
                x.MatchDate,
                x.Status,
                x.HomeGoals,
                x.AwayGoals,
                HomeTeam = x.HomeTeam != null ? new { x.HomeTeam.TeamId, x.HomeTeam.ApiTeamId, x.HomeTeam.TeamName } : null,
                AwayTeam = x.AwayTeam != null ? new { x.AwayTeam.TeamId, x.AwayTeam.ApiTeamId, x.AwayTeam.TeamName } : null,
            }));
        }

        [HttpGet("matches-with-teams")]
        public async Task<IActionResult> GetMatchesWithTeams([FromQuery] int? tournamentId = null, [FromQuery] int? seasonId = null)
        {
            var data = await _service.GetAllMatchesAsync(tournamentId, seasonId);
            var teamIds = data.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                              .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var teams = await _service.GetTeamsByIdsAsync(teamIds);
            var teamMap = teams.ToDictionary(t => t.TeamId);

            return Ok(data.Select(x => new
            {
                x.MatchId,
                x.ApiFixtureId,
                x.LeagueId,
                x.SeasonId,
                x.MatchDate,
                x.Status,
                x.HomeGoals,
                x.AwayGoals,
                x.HomePenalties,
                x.AwayPenalties,
                x.Round,
                HomeTeam = x.HomeTeamId.HasValue && teamMap.TryGetValue(x.HomeTeamId.Value, out var ht)
                    ? new { ht.TeamId, ht.ApiTeamId, ht.TeamName, ht.LogoUrl } : null,
                AwayTeam = x.AwayTeamId.HasValue && teamMap.TryGetValue(x.AwayTeamId.Value, out var at)
                    ? new { at.TeamId, at.ApiTeamId, at.TeamName, at.LogoUrl } : null,
            }));
        }

        [HttpGet("match-by-fixture")]
        public async Task<IActionResult> GetMatchByFixtureId([FromQuery] int apiFixtureId)
        {
            if (apiFixtureId <= 0) return BadRequest(new { error = "Invalid apiFixtureId" });
            var data = await _service.GetAllMatchesAsync();
            var x = data.FirstOrDefault(m => m.ApiFixtureId == apiFixtureId);
            if (x == null) return NotFound(new { error = "Match not found" });
            var teams = await _service.GetTeamsByIdsAsync(
                new[] { x.HomeTeamId, x.AwayTeamId }.Where(id => id.HasValue).Select(id => id!.Value).ToList());
            var teamMap = teams.ToDictionary(t => t.TeamId);
            return Ok(new
            {
                x.MatchId, x.ApiFixtureId, x.LeagueId, x.SeasonId,
                x.MatchDate, x.Status, x.HomeGoals, x.AwayGoals,
                x.HomePenalties, x.AwayPenalties, x.Round,
                HomeTeam = x.HomeTeamId.HasValue && teamMap.TryGetValue(x.HomeTeamId.Value, out var ht)
                    ? new { ht.TeamId, ht.ApiTeamId, ht.TeamName, ht.LogoUrl } : null,
                AwayTeam = x.AwayTeamId.HasValue && teamMap.TryGetValue(x.AwayTeamId.Value, out var at)
                    ? new { at.TeamId, at.ApiTeamId, at.TeamName, at.LogoUrl } : null,
            });
        }

        [HttpGet("matches")]
        public async Task<IActionResult> GetAllMatches()
        {
            var data = await _service.GetAllMatchesAsync();
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

        [HttpGet("players")]
        public async Task<IActionResult> GetAllPlayers()
        {
            try
            {
                var players = await _service.GetAllPlayersAsync();

                if (players == null || !players.Any())
                {
                    return NotFound(new
                    {
                        status = false,
                        message = "No players found in the database"
                    });
                }

                return Ok(new
                {
                    status = true,
                    message = $"Retrieved {players.Count} players successfully",
                    data = players
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all players");
                return StatusCode(500, new
                {
                    status = false,
                    message = "Internal server error"
                });
            }
        }

        [HttpGet("team-players")]
        public async Task<IActionResult> GetAllTeamPlayers([FromQuery] int? teamId, [FromQuery] int? sofascoreTeamId)
        {
            var data = await _service.GetAllPlayersAsync(teamId, sofascoreTeamId);
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

        [HttpGet("cuptree")]
        public async Task<IActionResult> GetCupTree([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { error = "Invalid parameters" });

            var cached = await _context.CupTrees
                .FirstOrDefaultAsync(c => c.TournamentId == tournamentId && c.SeasonId == seasonId);

            if (cached != null)
                return Content(cached.Data, "application/json");

            return NotFound(new { message = "Cup tree not synced yet. Call POST sync-cuptree first." });
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
                x.PenaltiesMissed,
                // GK stats
                x.Saves,
                x.SavesInsideBox,
                x.Punches,
                x.RunsOut,
                x.RunsOutSuccessful,
                x.HighClaims,
                x.GoalsConceded,
                x.PenaltiesSaved,
                x.CleanSheets
            }));
        }

        [HttpGet("match-events")]
        public async Task<IActionResult> GetAllMatchEvents()
        {
            var data = await _service.GetAllMatchEventsAsync();
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

        /// <summary>Bảng xếp hạng đã sync; tournamentId và seasonId là ID SofaScore (Api).</summary>
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
                ApiTeamId = x.Team != null ? x.Team.ApiTeamId : 0,
                TeamLogo = x.Team != null ? $"https://api.sofascore.app/api/v1/team/{x.Team.ApiTeamId}/image" : null,
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

        /// <summary>Đọc stats đã lưu. fetchIfEmpty=true: kéo từ SofaScore trước khi trả (cần trận đã có trong DB + cầu thủ đã sync).</summary>
        [HttpGet("player-match-stats-by-match")]
        public async Task<IActionResult> GetAllPlayerMatchStatsByMatch(
            [FromQuery] int apiFixtureId,
            [FromQuery] bool fetchIfEmpty = false)
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

            var data = await _service.GetAllPlayerMatchStatisticsByApiFixtureIdAsync(apiFixtureId, fetchIfEmpty);
            return Ok(data.Select(x => new
            {
                x.PlayerMatchStatId,
                x.MatchId,
                x.PlayerId,
                ApiPlayerId = x.Player != null ? x.Player.ApiPlayerId : (int?)null,
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
                x.UnsuccessfulTouch,
                // GK stats
                x.Saves,
                x.SavesInsideBox,
                x.Punches,
                x.RunsOut,
                x.RunsOutSuccessful,
                x.HighClaims,
                x.GoalsConceded,
                x.PenaltiesSaved
            }));
        }

        [HttpGet("player-match-stats-by-league-season")]
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
                x.UnsuccessfulTouch,
                // GK stats
                x.Saves,
                x.SavesInsideBox,
                x.Punches,
                x.RunsOut,
                x.RunsOutSuccessful,
                x.HighClaims,
                x.GoalsConceded,
                x.PenaltiesSaved
            }));
        }

        [HttpGet("lineups")]
        public async Task<IActionResult> GetAllLineupsByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            if (tournamentId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid tournamentId. Please provide a valid tournament ID."
                });
            }

            if (seasonId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid seasonId. Please provide a valid season ID."
                });
            }

            var data = await _service.GetAllLineupsByLeagueSeasonAsync(tournamentId, seasonId);

            if (!data.Any())
            {
                return NotFound(new
                {
                    status = false,
                    message = $"No lineups found for tournament {tournamentId} and season {seasonId}. Please sync lineups first."
                });
            }

            var result = data.Select(l => new
            {
                l.LineupId,
                l.MatchId,
                apiFixtureId = l.Match?.ApiFixtureId,
                matchDate = l.Match?.MatchDate,
                matchStatus = l.Match?.Status,
                round = l.Match?.Round,
                venue = l.Match?.Venue,
                homeTeam = new
                {
                    teamId = l.Match?.HomeTeamId,
                    teamName = l.Match?.HomeTeam?.TeamName,
                    teamLogo = l.Match?.HomeTeam?.LogoUrl
                },
                awayTeam = new
                {
                    teamId = l.Match?.AwayTeamId,
                    teamName = l.Match?.AwayTeam?.TeamName,
                    teamLogo = l.Match?.AwayTeam?.LogoUrl
                },
                score = l.Match?.HomeGoals != null || l.Match?.AwayGoals != null
                    ? $"{l.Match?.HomeGoals ?? 0} - {l.Match?.AwayGoals ?? 0}"
                    : null,
                teamId = l.TeamId,
                teamName = l.Team?.TeamName,
                teamLogo = l.Team?.LogoUrl,
                isHomeTeam = l.TeamId == l.Match?.HomeTeamId,
                l.Formation,
            });

            return Ok(new
            {
                status = true,
                message = $"Found {data.Count} lineups",
                data = result
            });
        }

        [HttpGet("contracts")]
        public async Task<IActionResult> GetContractsByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.GetContractsByLeagueSeasonAsync(tournamentId, seasonId);

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return NotFound(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contracts by league and season");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpGet("transfers")]
        public async Task<IActionResult> GetAllTransfers()
        {
            try
            {
                var result = await _service.GetAllTransfersAsync();

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return NotFound(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transfers");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
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

        [HttpPost("sync-player-season-stats-by-player")]
        public async Task<IActionResult> SyncPlayerStatsByPlayerId([FromQuery] int playerId)
        {
            if (playerId <= 0) return BadRequest(new { status = false, message = "Invalid playerId" });
            var result = await _service.SyncPlayerStatsByPlayerIdAsync(playerId);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                return Ok(result);
            return BadRequest(result);
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

        [HttpPost("sync-player-match-stats-by-match")]
        public async Task<IActionResult> FetchPlayerMatchStatsByApiMatchId([FromQuery] int apiFixtureId)
        {
            var result = await _service.FetchPlayerMatchStatsByApiMatchIdAsync(apiFixtureId);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("sync-player-match-stats-by-round")]
        public async Task<IActionResult> FetchPlayerMatchStatsByRound(
            [FromQuery] int tournamentId,
            [FromQuery] int seasonId,
            [FromQuery] string round)
        {
            if (tournamentId <= 0 || seasonId <= 0 || string.IsNullOrWhiteSpace(round))
                return BadRequest(new { status = false, message = "Invalid parameters" });
            var result = await _service.FetchPlayerMatchStatsByRoundAsync(tournamentId, seasonId, round);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status2 && status2)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("sync-player-match-stats-by-league-season")]
        public async Task<IActionResult> FetchPlayerMatchStatsByLeagueSeason(
            [FromQuery] int tournamentId,
            [FromQuery] int seasonId)
        {
            var result = await _service.FetchPlayerMatchStatsByLeagueSeasonAsync(tournamentId, seasonId);
            if (result.GetType().GetProperty("status")?.GetValue(result) is bool status && status)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("sync-lineups")]
        public async Task<IActionResult> SyncMatchLineups([FromQuery] int apiFixtureId)
        {
            if (apiFixtureId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid apiFixtureId. Please provide a valid fixture ID."
                });
            }

            var result = await _service.SyncMatchLineupsAsync(apiFixtureId);

            var resultType = result.GetType();
            var statusProp = resultType.GetProperty("status");
            var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

            if (!isSuccess)
            {
                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return NotFound(new { status = false, message });
            }

            return Ok(result);
        }

        [HttpPost("sync-all-lineups")]
        public async Task<IActionResult> FetchLineupsByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            if (tournamentId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid tournamentId. Please provide a valid tournament ID."
                });
            }

            if (seasonId <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid seasonId. Please provide a valid season ID."
                });
            }

            var result = await _service.FetchLineupsByLeagueSeasonAsync(tournamentId, seasonId);

            var resultType = result.GetType();
            var statusProp = resultType.GetProperty("status");
            var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

            if (!isSuccess)
            {
                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return NotFound(new { status = false, message });
            }

            return Ok(result);
        }

        [HttpPost("sync-contracts")]
        public async Task<IActionResult> SyncTeamContracts([FromQuery] int apiTeamId)
        {
            try
            {
                if (apiTeamId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid apiTeamId" });
                }

                var result = await _service.SyncTeamContractsAsync(apiTeamId);

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return BadRequest(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing contracts for team {ApiTeamId}", apiTeamId);
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-all-contracts")]
        public async Task<IActionResult> SyncAllTeamContractsByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.SyncAllTeamContractsByLeagueSeasonAsync(tournamentId, seasonId);

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return BadRequest(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all team contracts");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }


        [HttpPost("sync-transfers")]
        public async Task<IActionResult> SyncTeamTransfers([FromQuery] int apiTeamId)
        {
            try
            {
                if (apiTeamId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid apiTeamId" });
                }

                var result = await _service.SyncTeamTransfersAsync(apiTeamId);

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return BadRequest(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing transfers for team {ApiTeamId}", apiTeamId);
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-all-transfers")]
        public async Task<IActionResult> SyncAllTeamTransfersByLeagueSeason(
        [FromQuery] int tournamentId,
        [FromQuery] int seasonId)
        {
            try
            {
                if (tournamentId <= 0 || seasonId <= 0)
                {
                    return BadRequest(new { status = false, message = "Invalid tournamentId or seasonId" });
                }

                var result = await _service.SyncAllTeamTransfersByLeagueSeasonAsync(tournamentId, seasonId);

                var resultType = result.GetType();
                var statusProp = resultType.GetProperty("status");
                var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                if (isSuccess)
                {
                    return Ok(result);
                }

                var messageProp = resultType.GetProperty("message");
                var message = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";
                return BadRequest(new { status = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all team transfers");
                return StatusCode(500, new { status = false, message = "Internal server error" });
            }
        }

        [HttpPost("sync-cuptree")]
        public async Task<IActionResult> SyncCupTree([FromQuery] int tournamentId, [FromQuery] int seasonId)
        {
            if (tournamentId <= 0 || seasonId <= 0)
                return BadRequest(new { error = "Invalid parameters" });
            try
            {
                var json = await _sofascoreScraperService.GetTournamentCupTreesAsync(tournamentId, seasonId);
                var existing = await _context.CupTrees
                    .FirstOrDefaultAsync(c => c.TournamentId == tournamentId && c.SeasonId == seasonId);
                if (existing == null)
                {
                    _context.CupTrees.Add(new VNFootballLeagues.Repositories.Models.CupTree
                    {
                        TournamentId = tournamentId, SeasonId = seasonId,
                        Data = json, LastUpdated = DateTime.UtcNow
                    });
                }
                else { existing.Data = json; existing.LastUpdated = DateTime.UtcNow; }
                await _context.SaveChangesAsync();
                return Ok(new { status = true, message = "Cup tree synced successfully" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
    }
}