using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Models.Api.GetPlayer;

namespace VNFootballLeagues.Services.IServices
{
    public interface IFootballApiService
    {
        // Sync methods (POST)
        Task<List<Team>> SyncTeamsByLeagueAsync(int apiLeagueId, int season);
        Task<List<Season>> SyncSeasonsAsync();
        Task<List<League>> SyncLeaguesAsync();
        Task<List<Player>> SyncPlayersByLeagueAsync(int apiLeagueId, int seasonYear);
        Task<List<PlayerSeasonStatistic>> SyncPlayerSeasonStatisticsAsync(int apiLeagueId, int seasonYear);
        Task<List<Match>> SyncMatchesByLeagueAsync(int apiLeagueId, int season);
        Task<List<Standing>> SyncStandingsAsync(int apiLeagueId, int seasonYear);

        // Get methods (GET)
        Task<List<League>> GetLeaguesAsync();
        Task<List<Season>> GetSeasonsAsync(int? leagueId = null);
        Task<List<Team>> GetTeamsAsync(int? leagueId = null);
        Task<List<Player>> GetPlayersAsync(int? teamId = null);
        Task<List<PlayerSeasonStatistic>> GetPlayerStatsAsync(int? playerId = null, int? seasonId = null);
        Task<List<Match>> GetMatchesAsync(int? leagueId = null, int? seasonId = null);
        Task<List<Standing>> GetStandingsAsync(int? leagueId = null, int? seasonId = null);
    }
}
