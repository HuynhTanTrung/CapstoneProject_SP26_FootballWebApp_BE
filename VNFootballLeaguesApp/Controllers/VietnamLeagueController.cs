using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.DTOs.Football;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FootballController : ControllerBase
    {
        private static readonly Regex RoundNumberRegex = new(@"\d+", RegexOptions.Compiled);
        private readonly IFootballApiService _service;
        private readonly VNFootballLeaguesDBContext _db;

        public FootballController(IFootballApiService service, VNFootballLeaguesDBContext db)
        {
            _service = service;
            _db = db;
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
            var x = await _service.GetPlayerByIdAsync(id);
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
                    m.Round ?? string.Empty))
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
                    s.Player != null ? s.Player.PhotoUrl : null))
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
