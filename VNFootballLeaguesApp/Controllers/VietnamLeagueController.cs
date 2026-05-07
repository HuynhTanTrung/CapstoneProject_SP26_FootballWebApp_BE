using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.DTOs.Football;

namespace VNFootballLeaguesApp.Controllers
{
    public record IdentifyPlayersRequest(string Text);
    [ApiController]
    [Route("api/[controller]")]
    public class FootballController : ControllerBase
    {
        private static readonly Regex RoundNumberRegex = new(@"\d+", RegexOptions.Compiled);
        private readonly IFootballApiService _service;
        private readonly VNFootballLeaguesDBContext _db;
        private readonly IGeminiService _gemini;
        private readonly ILogger<FootballController> _logger;

        public FootballController(IFootballApiService service, VNFootballLeaguesDBContext db, IGeminiService gemini, ILogger<FootballController> logger)
        {
            _service = service;
            _db = db;
            _gemini = gemini;
            _logger = logger;
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

        //[HttpPost("sync-transfers")]
        //public async Task<IActionResult> SyncTransfers(
        //[FromQuery] int apiTeamId)
        //{
        //    var transfers = await _service.SyncTransfersAsync(apiTeamId);

        //    return Ok(new
        //    {
        //        success = true,
        //        message = "Transfers synced successfully",
        //        count = transfers.Count
        //    });
        //}

        [HttpPost("sync-team-statistics")]
        public async Task<IActionResult> SyncTeamStatistics(
        [FromQuery] int apiLeagueId,
        [FromQuery] int season,
        [FromQuery] int apiTeamId)
        {
            try
            {
                var stat = await _service.SyncTeamStatisticsAsync(apiLeagueId, season, apiTeamId);

                if (stat == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No team statistics found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Team statistics synced successfully",
                    data = new
                    {
                        stat.TeamStatId,
                        stat.TeamId,
                        stat.LeagueId,
                        stat.SeasonId,
                        stat.Form,
                        stat.Played,
                        stat.Wins,
                        stat.Draws,
                        stat.Losses,
                        stat.HomePlayed,
                        stat.HomeWins,
                        stat.HomeDraws,
                        stat.HomeLosses,
                        stat.AwayPlayed,
                        stat.AwayWins,
                        stat.AwayDraws,
                        stat.AwayLosses,
                        stat.GoalsFor,
                        stat.GoalsAgainst,
                        stat.HomeGoalsFor,
                        stat.AwayGoalsFor,
                        stat.HomeGoalsAgainst,
                        stat.AwayGoalsAgainst,
                        stat.CleanSheets,
                        stat.CleanSheetsHome,
                        stat.CleanSheetsAway,
                        stat.FailedToScore,
                        stat.FailedToScoreHome,
                        stat.FailedToScoreAway,
                        stat.PenaltiesScored,
                        stat.PenaltiesMissed,
                        stat.PenaltiesTotal,
                        stat.PenaltyPercentage,
                        stat.BiggestStreakWins,
                        stat.BiggestStreakDraws,
                        stat.BiggestStreakLosses
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
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

        // ==================== GetAll Endpoints ====================

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
        public async Task<IActionResult> GetAllSeasons([FromQuery] int? leagueId)
        {
            var data = await _service.GetAllSeasonsAsync();
            if (leagueId.HasValue)
                data = data.Where(s => s.LeagueId == leagueId.Value).ToList();
            return Ok(data.Select(x => new
            {
                x.SeasonId,
                x.LeagueId,
                x.Year,
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
                x.ApiTeamId,
                x.LogoUrl,
                x.ShortName,
                x.Founded,
                x.National,
                x.StadiumId,
                x.LeagueId
            }));
        }

        [HttpGet("teams/{id}")]
        public async Task<IActionResult> GetTeamById(int id)
        {
            var x = await _service.GetTeamByIdAsync(id);
            if (x == null) return NotFound();
            return Ok(new
            {
                x.TeamId,
                x.TeamName,
                x.ApiTeamId,
                x.LogoUrl,
                x.ShortName,
                x.Founded,
                x.National,
                x.StadiumId,
                x.LeagueId,
                Stadium = x.Stadium == null ? null : new
                {
                    x.Stadium.StadiumId,
                    x.Stadium.StadiumName,
                    x.Stadium.City,
                    x.Stadium.Capacity,
                    x.Stadium.Surface,
                    x.Stadium.ImageUrl,
                    x.Stadium.Address
                }
            });
        }

        [HttpGet("players")]
        public async Task<IActionResult> GetAllPlayers([FromQuery] int? teamId)
        {
            var data = await _service.GetAllPlayersAsync(teamId);
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

        [HttpGet("players/{id}")]
        public async Task<IActionResult> GetPlayerById(int id)
        {
            // Try internal PlayerId first, then ApiPlayerId
            var x = await _service.GetPlayerByIdAsync(id);
            if (x == null)
                x = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.ApiPlayerId == id);
            if (x == null) return NotFound();
            return Ok(new
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
            });
        }

        [HttpGet("player-stats/by-player/{playerId}")]
        public async Task<IActionResult> GetPlayerStatsByPlayerId(int playerId)
        {
            var data = await _service.GetPlayerStatsByPlayerIdAsync(playerId);
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
                x.CleanSheets,
                x.GoalsConceded,
                x.PenaltiesSaved,
                x.Punches,
                x.RunsOut,
                x.RunsOutSuccessful,
                x.HighClaims
            }));
        }

        [HttpGet("players/compare-stats")]
        public async Task<IActionResult> ComparePlayers([FromQuery] int player1Id, [FromQuery] int player2Id)
        {
            var p1 = await _service.GetPlayerByIdAsync(player1Id);
            var p2 = await _service.GetPlayerByIdAsync(player2Id);
            if (p1 == null || p2 == null) return NotFound(new { message = "One or both players not found" });

            var stats1 = await _service.GetPlayerStatsByPlayerIdAsync(player1Id);
            var stats2 = await _service.GetPlayerStatsByPlayerIdAsync(player2Id);

            object MapPlayer(VNFootballLeagues.Repositories.Models.Player p, List<VNFootballLeagues.Repositories.Models.PlayerSeasonStatistic> stats) => new
            {
                p.PlayerId,
                p.FullName,
                p.PhotoUrl,
                p.Position,
                p.Nationality,
                p.Age,
                p.TeamId,
                Statistics = stats.Select(x => new
                {
                    x.PlayerStatisticsId,
                    x.SeasonId,
                    x.LeagueId,
                    x.TeamId,
                    x.Appearances,
                    x.Lineups,
                    x.Minutes,
                    x.Goals,
                    x.Assists,
                    x.YellowCards,
                    x.RedCards,
                    x.Rating,
                    x.ShotsTotal,
                    x.ShotsOnTarget,
                    x.PassesTotal,
                    x.PassesKey,
                    x.PassesAccuracy,
                    x.DribblesAttempted,
                    x.DribblesSuccess,
                    x.DuelsWon,
                    x.DuelsTotal,
                    x.Tackles,
                    x.Interceptions,
                    x.FoulsCommitted,
                    x.FoulsDrawn,
                    x.PenaltiesScored,
                    x.PenaltiesMissed,
                    x.Saves,
                    x.SavesInsideBox,
                    x.CleanSheets,
                    x.GoalsConceded,
                    x.PenaltiesSaved,
                })
            };

            return Ok(new
            {
                Player1 = MapPlayer(p1, stats1),
                Player2 = MapPlayer(p2, stats2),
            });
        }

        [HttpGet("player-stats")]
        public async Task<IActionResult> GetAllPlayerStats()
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

        [HttpGet("standings")]
        public async Task<IActionResult> GetAllStandings()
        {
            var data = await _service.GetAllStandingsAsync();
            return Ok(data.Select(x => new
            {
                x.StandingId,
                x.LeagueId,
                x.SeasonId,
                x.TeamId,
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

        [Authorize]
        [HttpGet("rounds")]
        public async Task<IActionResult> GetRounds([FromQuery] int seasonId)
        {
            if (seasonId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "seasonId không hợp lệ"
                });
            }

            var rounds = await _db.Matches
                .AsNoTracking()
                .Where(m => m.SeasonId == seasonId && !string.IsNullOrWhiteSpace(m.Round))
                .GroupBy(m => m.Round!)
                .Select(g => new RoundDto(g.Key, g.Count()))
                .ToListAsync(HttpContext.RequestAborted);

            var orderedRounds = rounds
                .OrderBy(r => ExtractRoundNumber(r.Round) ?? int.MaxValue)
                .ThenBy(r => r.Round, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(orderedRounds);
        }

        [Authorize]
        [HttpGet("matches/by-round")]
        public async Task<IActionResult> GetMatchesByRound([FromQuery] int seasonId, [FromQuery] string round)
        {
            if (seasonId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "seasonId không hợp lệ"
                });
            }

            if (string.IsNullOrWhiteSpace(round))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "round là bắt buộc"
                });
            }

            var matches = await _db.Matches
                .AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.SeasonId == seasonId && m.Round == round)
                .OrderBy(m => m.MatchDate)
                .ThenBy(m => m.MatchId)
                .Select(m => new MatchListItemDto(
                    m.MatchId,
                    m.MatchDate,
                    m.HomeTeamId,
                    m.HomeTeam != null ? m.HomeTeam.TeamName ?? string.Empty : string.Empty,
                    m.AwayTeamId,
                    m.AwayTeam != null ? m.AwayTeam.TeamName ?? string.Empty : string.Empty,
                    m.HomeGoals,
                    m.AwayGoals,
                    m.Status ?? string.Empty,
                    m.Round ?? string.Empty,
                    m.HomeTeam != null ? m.HomeTeam.ApiTeamId : null,
                    m.AwayTeam != null ? m.AwayTeam.ApiTeamId : null))
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(matches);
        }

        [Authorize]
        [HttpGet("matches/{matchId:int}/players")]
        public async Task<IActionResult> GetPlayersByMatch(int matchId)
        {
            if (matchId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "matchId không hợp lệ"
                });
            }

            var players = await _db.PlayerMatchStatistics
                .AsNoTracking()
                .Include(s => s.Player)
                .Include(s => s.Team)
                .Where(s => s.MatchId == matchId && s.PlayerId.HasValue)
                .OrderBy(s => s.Team != null ? s.Team.TeamName : string.Empty)
                .ThenByDescending(s => s.Rating ?? s.SofascoreRating ?? 0)
                .ThenBy(s => s.Player != null ? s.Player.FullName : string.Empty)
                .Select(s => new PlayerInMatchDto(
                    s.PlayerId ?? 0,
                    s.Player != null ? s.Player.FullName ?? string.Empty : string.Empty,
                    s.TeamId,
                    s.Team != null ? s.Team.TeamName ?? string.Empty : string.Empty,
                    s.Player != null ? s.Player.Position ?? string.Empty : string.Empty,
                    s.Rating ?? s.SofascoreRating,
                    s.Minutes,
                    s.Player != null ? s.Player.PhotoUrl : null,
                    s.Player != null ? s.Player.ApiPlayerId : null))
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(players);
        }

        [Authorize]
        [HttpGet("matches/{matchId:int}/events")]
        public async Task<IActionResult> GetMatchEventsByMatch(int matchId)
        {
            if (matchId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "matchId không hợp lệ"
                });
            }

            var events = await _db.MatchEvents
                .AsNoTracking()
                .Include(e => e.Player)
                .Include(e => e.Team)
                .Where(e => e.MatchId == matchId)
                .ToListAsync(HttpContext.RequestAborted);

            var assistPlayerIds = events
                .Where(e => e.AssistPlayerId.HasValue)
                .Select(e => e.AssistPlayerId!.Value)
                .Distinct()
                .ToList();

            var assistPlayers = assistPlayerIds.Count == 0
                ? new Dictionary<int, string>()
                : await _db.Players
                    .AsNoTracking()
                    .Where(p => assistPlayerIds.Contains(p.PlayerId))
                    .ToDictionaryAsync(p => p.PlayerId, p => p.FullName ?? string.Empty, HttpContext.RequestAborted);

            var orderedEvents = events
                .OrderBy(e => GetPeriodSortOrder(e.Period))
                .ThenBy(e => e.EventTime ?? int.MaxValue)
                .ThenBy(e => e.ExtraTime ?? 0)
                .Select(e => new MatchEventDto(
                    e.EventId,
                    e.MatchId,
                    e.TeamId,
                    e.Team?.TeamName ?? string.Empty,
                    e.PlayerId,
                    e.Player?.FullName ?? string.Empty,
                    e.AssistPlayerId,
                    e.AssistPlayerId.HasValue && assistPlayers.TryGetValue(e.AssistPlayerId.Value, out var assistName)
                        ? assistName
                        : string.Empty,
                    e.EventType ?? string.Empty,
                    e.Detail ?? string.Empty,
                    e.EventTime,
                    e.ExtraTime,
                    e.Period ?? string.Empty,
                    e.Comments ?? string.Empty))
                .ToList();

            return Ok(orderedEvents);
        }

        //[HttpGet("transfers")]
        //public async Task<IActionResult> GetAllTransfers()
        //{
        //    var data = await _service.GetAllTransfersAsync();
        //    return Ok(data.Select(x => new
        //    {
        //        x.TransferId,
        //        x.PlayerId,
        //        x.FromTeamId,
        //        x.ToTeamId,
        //        x.TransferDate,
        //        x.TransferType
        //    }));
        //}

        [HttpGet("team-statistics")]
        public async Task<IActionResult> GetAllTeamStatistics()
        {
            var data = await _service.GetAllTeamStatisticsAsync();
            return Ok(data.Select(x => new
            {
                x.TeamStatId,
                x.TeamId,
                x.LeagueId,
                x.SeasonId,
                x.Played,
                x.Wins,
                x.Draws,
                x.Losses,
                x.GoalsFor,
                x.GoalsAgainst,
                x.Form,
                x.HomePlayed,
                x.HomeWins,
                x.HomeDraws,
                x.HomeLosses,
                x.AwayPlayed,
                x.AwayWins,
                x.AwayDraws,
                x.AwayLosses,
                x.HomeGoalsFor,
                x.AwayGoalsFor,
                x.HomeGoalsAgainst,
                x.AwayGoalsAgainst,
                x.GoalsForAvgHome,
                x.GoalsForAvgAway,
                x.GoalsForAvgTotal,
                x.GoalsAgainstAvgHome,
                x.GoalsAgainstAvgAway,
                x.GoalsAgainstAvgTotal,
                x.GoalsForMinute,
                x.GoalsAgainstMinute,
                x.UnderOverFor,
                x.UnderOverAgainst,
                x.BiggestStreakWins,
                x.BiggestStreakDraws,
                x.BiggestStreakLosses,
                x.BiggestWinHome,
                x.BiggestWinAway,
                x.BiggestLossHome,
                x.BiggestLossAway,
                x.BiggestGoalsForHome,
                x.BiggestGoalsForAway,
                x.BiggestGoalsAgainstHome,
                x.BiggestGoalsAgainstAway,
                x.PenaltiesScored,
                x.PenaltiesMissed,
                x.PenaltiesTotal,
                x.PenaltyPercentage,
                x.YellowCardsMinute,
                x.RedCardsMinute,
                x.CleanSheets,
                x.CleanSheetsHome,
                x.CleanSheetsAway,
                x.FailedToScore,
                x.FailedToScoreHome,
                x.FailedToScoreAway
            }));
        }

        /// <summary>Identify football players from selected text using AI</summary>
        [HttpPost("identify-players")]
        [AllowAnonymous]
        public async Task<IActionResult> IdentifyPlayers([FromBody] IdentifyPlayersRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { success = false, message = "Text is required" });

            var text = request.Text.Trim();
            var textLower = text.ToLower();
            var textNorm = RemoveDiacritics(textLower); // for normalized matching

            // Load all players - select only needed fields to reduce query size
            var allPlayers = await _db.Players.AsNoTracking()
                .Include(p => p.Team)
                .Where(p => p.FullName != null)
                .Select(p => new {
                    p.PlayerId, p.ApiPlayerId, p.FullName, p.Position,
                    p.Nationality, p.PhotoUrl, p.Age, p.Number,
                    TeamName = p.Team != null ? p.Team.TeamName : null
                })
                .ToListAsync();

            // Helper: check if pattern exists in text with word boundaries
            bool ContainsWithBoundary(string text, string pattern)
            {
                if (string.IsNullOrEmpty(pattern)) return false;
                int idx = text.IndexOf(pattern, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    bool leftOk = idx == 0 || !char.IsLetter(text[idx - 1]);
                    bool rightOk = idx + pattern.Length >= text.Length || !char.IsLetter(text[idx + pattern.Length]);
                    if (leftOk && rightOk) return true;
                    idx = text.IndexOf(pattern, idx + 1, StringComparison.Ordinal);
                }
                return false;
            }

            var found = allPlayers
                .Where(p => {
                    var fullLower = p.FullName!.ToLower();
                    var fullNorm = RemoveDiacritics(fullLower);
                    var parts = fullLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var partsNorm = fullNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // 1. Full name match (with word boundary)
                    if (fullLower.Length >= 5 && ContainsWithBoundary(textLower, fullLower)) return true;
                    if (fullNorm.Length >= 5 && ContainsWithBoundary(textNorm, fullNorm)) return true;

                    // 2. Suffix match (e.g. "minh khoa" from "võ hoàng minh khoa")
                    //    Require at least 2 words AND 8+ chars, with word boundary
                    //    Exclude common place names / generic terms that cause false positives
                    var excludedSuffixes = new HashSet<string> { "thanh nam", "thanh hoa", "thanh pho", "ha noi", "ho chi minh", "da nang", "binh duong", "nam dinh" };
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var suffix = string.Join(" ", parts.Skip(i));
                        var suffixNorm = string.Join(" ", partsNorm.Skip(i));
                        var suffixParts = suffix.Split(' ');
                        if (excludedSuffixes.Contains(suffixNorm)) continue; // skip common place names
                        if (suffixParts.Length >= 2 && suffix.Length >= 8 && ContainsWithBoundary(textLower, suffix)) return true;
                        if (suffixParts.Length >= 2 && suffixNorm.Length >= 8 && ContainsWithBoundary(textNorm, suffixNorm)) return true;
                    }

                    // 3. Reversed name match for foreign players (e.g. "Nguyễn Filip" vs "Filip Nguyen")
                    //    Only for names with no Vietnamese diacritics (foreign names)
                    if (parts.Length >= 2)
                    {
                        var firstPart = parts[0];
                        var isLikelyForeign = RemoveDiacritics(firstPart) == firstPart; // no diacritics
                        if (isLikelyForeign)
                        {
                            // Match first name alone (e.g. "Chadrac" from "Chadrac Akolo")
                            // Use word boundary: check char before/after match
                            // Exclude common Vietnamese surnames used as first names in foreign player names
                            var commonSurnames = new HashSet<string> { "nguyen", "tran", "le", "pham", "hoang", "huynh", "vo", "vu", "dang", "bui", "do", "ho", "ngo", "duong", "ly" };
                            if (firstPart.Length >= 6 && !commonSurnames.Contains(firstPart))
                            {
                                var idx = textNorm.IndexOf(firstPart, StringComparison.Ordinal);
                                while (idx >= 0)
                                {
                                    var leftOk = idx == 0 || !char.IsLetter(textNorm[idx - 1]);
                                    var rightOk = idx + firstPart.Length >= textNorm.Length || !char.IsLetter(textNorm[idx + firstPart.Length]);
                                    if (leftOk && rightOk) return true;
                                    idx = textNorm.IndexOf(firstPart, idx + 1, StringComparison.Ordinal);
                                }
                            }

                            // Match reversed full name (e.g. "Nguyen Filip" → "Filip Nguyen")
                            var reversed = string.Join(" ", partsNorm.Reverse());
                            if (reversed.Length >= 10 && textNorm.Contains(reversed)) return true;
                        }
                    }

                    return false;
                })
                .Take(8)
                .Select(p => new {
                    extractedName = p.FullName,
                    found = true,
                    player = new {
                        p.PlayerId,
                        p.ApiPlayerId,
                        p.FullName,
                        p.Position,
                        p.Nationality,
                        p.PhotoUrl,
                        p.Age,
                        p.Number,
                        teamName = p.TeamName,
                        photoProxyUrl = p.ApiPlayerId.HasValue ? $"/api/ImageProxy/sofascore/player/{p.ApiPlayerId}" : null,
                        profileUrl = $"/players/{p.ApiPlayerId}"
                    }
                })
                .ToList();

            return Ok(new { success = true, count = found.Count, players = found });
        }
        /// <summary>Identify a team from selected text using fuzzy matching</summary>
        [HttpPost("identify-team")]
        [AllowAnonymous]
        public async Task<IActionResult> IdentifyTeam([FromBody] IdentifyPlayersRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { success = false });

            var text = request.Text.Trim();
            var textLower = text.ToLower();
            var textNorm = RemoveDiacritics(textLower);

            var teams = await _db.Teams.AsNoTracking()
                .Include(t => t.Stadium)
                .ToListAsync();

            // Score each team
            var scored = teams.Select(t => {
                int score = 0;
                var nameNorm = RemoveDiacritics(t.TeamName?.ToLower() ?? "");
                var shortNorm = RemoveDiacritics(t.ShortName?.ToLower() ?? "");

                // Exact full name match
                if (!string.IsNullOrEmpty(nameNorm) && textNorm == nameNorm) score += 100;
                else if (!string.IsNullOrEmpty(nameNorm) && textNorm.Contains(nameNorm)) score += nameNorm.Length * 4;

                // Short name match
                if (!string.IsNullOrEmpty(shortNorm) && textNorm == shortNorm) score += 80;
                else if (!string.IsNullOrEmpty(shortNorm) && textNorm.Contains(shortNorm)) score += shortNorm.Length * 3;

                return new { Team = t, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

            if (scored == null)
                return Ok(new { success = false, team = (object?)null });

            var t2 = scored.Team;
            // Get standings for current V-League season
            // SeasonId 1-10 are real seasons; higher IDs are from sync artifacts
            var standing = await _db.Standings.AsNoTracking()
                .Where(s => s.TeamId == t2.TeamId && s.LeagueId == 1 && s.SeasonId <= 10)
                .OrderByDescending(s => s.SeasonId)
                .FirstOrDefaultAsync();

            return Ok(new {
                success = true,
                team = new {
                    t2.TeamId,
                    t2.ApiTeamId,
                    t2.TeamName,
                    t2.ShortName,
                    t2.LogoUrl,
                    logoProxyUrl = t2.ApiTeamId.HasValue ? $"/api/ImageProxy/sofascore/team/{t2.ApiTeamId}" : null,
                    t2.Founded,
                    stadiumName = t2.Stadium?.StadiumName,
                    stadiumCity = t2.Stadium?.City,
                    profileUrl = $"/teams/{t2.TeamId}",
                    standing = standing == null ? null : new {
                        standing.Rank,
                        standing.Played,
                        standing.Points,
                        standing.Win,
                        standing.Draw,
                        standing.Loss,
                        standing.GoalsFor,
                        standing.GoalsAgainst,
                        standing.Form,
                    }
                }
            });
        }

        private static VNFootballLeagues.Repositories.Models.Match? PickBestMatch(
            List<VNFootballLeagues.Repositories.Models.Match> candidates,
            int? mentionedRound)
        {
            if (!candidates.Any()) return null;

            if (mentionedRound.HasValue)
            {
                var byRound = candidates.FirstOrDefault(m =>
                    m.Round != null && ExtractRoundNumber(m.Round) == mentionedRound.Value);
                if (byRound != null) return byRound;
            }

            // Prefer finished matches with actual score over upcoming ones
            var finished = candidates
                .Where(m => m.HomeGoals != null && m.AwayGoals != null)
                .OrderByDescending(m => m.MatchDate)
                .FirstOrDefault();
            if (finished != null) return finished;

            return candidates.First();
        }

        [HttpPost("identify-match")]
        [AllowAnonymous]
        public async Task<IActionResult> IdentifyMatch([FromBody] IdentifyPlayersRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { success = false });

            var text = request.Text.Trim();
            var textLower = text.ToLower();
            var textNorm = RemoveDiacritics(textLower);
            var teams = await _db.Teams.AsNoTracking().ToListAsync();

            // Extract "lead text" = title + first 2 sentences (most likely to contain the main match teams)
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var leadText = string.Join(". ", sentences.Take(2)).Trim();
            var leadLower = leadText.ToLower();
            var leadNorm = RemoveDiacritics(leadLower);

            // Score each team — weight lead text 3x higher than full text
            var scored = teams.Select(t => {
                int score = 0;
                var nameNorm = RemoveDiacritics(t.TeamName?.ToLower() ?? "");
                var shortNorm = RemoveDiacritics(t.ShortName?.ToLower() ?? "");

                // Lead text match (high weight — these are the main match teams)
                if (!string.IsNullOrEmpty(nameNorm) && leadNorm.Contains(nameNorm))
                    score += nameNorm.Length * 10;
                if (!string.IsNullOrEmpty(shortNorm) && leadNorm.Contains(shortNorm))
                    score += shortNorm.Length * 8;

                // Full text match (lower weight — may include context teams)
                if (!string.IsNullOrEmpty(nameNorm) && textNorm.Contains(nameNorm))
                    score += nameNorm.Length * 2;
                if (!string.IsNullOrEmpty(shortNorm) && textNorm.Contains(shortNorm))
                    score += shortNorm.Length * 1;

                return new { Team = t, Score = score };
            }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToList();

            // Remove teams whose full name is a substring of a higher-scored team's name
            // e.g. "Hà Nội" should be excluded when "Công An Hà Nội" is already matched
            var filteredScored = scored.ToList();
            for (int i = filteredScored.Count - 1; i >= 0; i--)
            {
                var candidate = RemoveDiacritics(filteredScored[i].Team.TeamName?.ToLower() ?? "");
                bool isSubsetOfHigherScored = filteredScored
                    .Take(i)
                    .Any(other =>
                    {
                        var otherName = RemoveDiacritics(other.Team.TeamName?.ToLower() ?? "");
                        return otherName.Contains(candidate) && candidate.Length < otherName.Length;
                    });
                if (isSubsetOfHigherScored)
                    filteredScored.RemoveAt(i);
            }
            scored = filteredScored;

            var matchedTeams = scored.Take(2).Select(x => x.Team).ToList();
            if (!matchedTeams.Any())
                return Ok(new { success = false, match = (object?)null });

            // Try to extract round number from text (e.g. "vòng 19", "round 19", "vong 19")
            int? mentionedRound = null;
            var roundPatterns = new[]
            {
                new Regex(@"vong\s+(\d+)", RegexOptions.IgnoreCase),   // matches normalized "vong 19"
                new Regex(@"round\s+(\d+)", RegexOptions.IgnoreCase),
                new Regex(@"luot\s+\d+\s+vong\s+(\d+)", RegexOptions.IgnoreCase),
            };
            foreach (var pattern in roundPatterns)
            {
                // Always match against textNorm (diacritics removed) for reliability
                var m = pattern.Match(textNorm);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var rn))
                {
                    mentionedRound = rn;
                    break;
                }
            }

            VNFootballLeagues.Repositories.Models.Match? match;
            if (matchedTeams.Count >= 2)
            {
                var t1 = matchedTeams[0].TeamId; var t2 = matchedTeams[1].TeamId;
                var candidates = await _db.Matches.AsNoTracking()
                    .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                    .Where(m => (m.HomeTeamId == t1 && m.AwayTeamId == t2) || (m.HomeTeamId == t2 && m.AwayTeamId == t1))
                    .OrderByDescending(m => m.MatchDate)
                    .ToListAsync();
                match = PickBestMatch(candidates, mentionedRound);
            }
            else
            {
                // Only 1 team found — this is likely a general article, not a match report
                // Don't return a match; let the caller show team/player info instead
                return Ok(new { success = false, match = (object?)null });
            }

            if (match == null)
                return Ok(new { success = false, match = (object?)null });

            return Ok(new {
                success = true,
                match = new {
                    match.MatchId, match.ApiFixtureId, match.MatchDate, match.Status,
                    match.HomeGoals, match.AwayGoals, match.Round, match.Venue,
                    homeTeam = new { match.HomeTeam?.TeamId, match.HomeTeam?.TeamName, match.HomeTeam?.LogoUrl },
                    awayTeam = new { match.AwayTeam?.TeamId, match.AwayTeam?.TeamName, match.AwayTeam?.LogoUrl },
                    profileUrl = $"/matches/{match.ApiFixtureId}"
                }
            });
        }

        [HttpGet("players/{id}/photo")]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400)]
        public async Task<IActionResult> GetPlayerPhoto(int id)
        {
            var player = await _db.Players.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PlayerId == id || p.ApiPlayerId == id);
            if (player?.PhotoUrl == null) return NotFound();

            try
            {
                var http = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                http.DefaultRequestHeaders.Add("Referer", "https://www.sofascore.com/");
                var bytes = await http.GetByteArrayAsync(player.PhotoUrl);
                return File(bytes, "image/jpeg");
            }
            catch { return NotFound(); }
        }

        [HttpGet("teams/{id}/logo")]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400)]
        public async Task<IActionResult> GetTeamLogo(int id)
        {
            var team = await _db.Teams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TeamId == id);
            if (team?.LogoUrl == null) return NotFound();

            try
            {
                var http = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                http.DefaultRequestHeaders.Add("Referer", "https://www.sofascore.com/");
                var bytes = await http.GetByteArrayAsync(team.LogoUrl);
                return File(bytes, "image/png");
            }
            catch { return NotFound(); }
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private static int? ExtractRoundNumber(string round)
        {
            var match = RoundNumberRegex.Match(round);
            return match.Success && int.TryParse(match.Value, out var value) ? value : null;
        }

        private static int GetPeriodSortOrder(string? period)
        {
            if (string.IsNullOrWhiteSpace(period))
            {
                return 99;
            }

            var normalized = period.Trim().ToLowerInvariant();
            return normalized switch
            {
                "1st half" => 1,
                "first half" => 1,
                "2nd half" => 2,
                "second half" => 2,
                "regular" => 2,
                "extra time" => 3,
                "penalties" => 4,
                _ => 99
            };
        }
    }
}





