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
        public async Task<IActionResult> GetAllSeasons()
        {
            var data = await _service.GetAllSeasonsAsync();
            return Ok(data.Select(x => new
            {
                x.SeasonId,
                x.LeagueId,
                x.Year,
                x.StartDate,
                x.EndDate,
                x.IsCurrent,
                x.IsCurrentSeason,
                x.ApiCoverage
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

        [HttpGet("players")]
        public async Task<IActionResult> GetAllPlayers()
        {
            var data = await _service.GetAllPlayersAsync();
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

        [HttpGet("transfers")]
        public async Task<IActionResult> GetAllTransfers()
        {
            var data = await _service.GetAllTransfersAsync();
            return Ok(data.Select(x => new
            {
                x.TransferId,
                x.PlayerId,
                x.FromTeamId,
                x.ToTeamId,
                x.TransferDate,
                x.TransferType
            }));
        }

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
    }
}
