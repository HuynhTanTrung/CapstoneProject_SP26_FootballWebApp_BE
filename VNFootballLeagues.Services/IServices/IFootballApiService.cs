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
        Task<List<Team>> SyncTeamsByLeagueAsync(int apiLeagueId, int season);
        Task<List<Season>> SyncSeasonsAsync();
        Task<List<League>> SyncLeaguesAsync();
        Task<List<Player>> SyncPlayersByLeagueAsync(int apiLeagueId, int seasonYear);
        Task<List<PlayerSeasonStatistic>> SyncPlayerSeasonStatisticsAsync(int apiLeagueId, int seasonYear);
        Task<List<Match>> SyncMatchesByLeagueAsync(int apiLeagueId, int season);
        Task<List<Standing>> SyncStandingsAsync(int apiLeagueId, int seasonYear);
        Task<List<MatchEvent>> SyncMatchEventsAsync(int apiFixtureId);
        Task<List<Transfer>> SyncTransfersAsync(int apiTeamId);
        Task<TeamStatistic> SyncTeamStatisticsAsync(int apiLeagueId, int seasonYear, int apiTeamId);
        //Task<List<Lineup>> SyncLineupsAsync(int apiFixtureId);

        // GetAll methods
        Task<List<League>> GetAllLeaguesAsync();
        Task<List<Season>> GetAllSeasonsAsync();
        Task<List<Team>> GetAllTeamsAsync();
        Task<List<Player>> GetAllPlayersAsync();
        Task<List<PlayerSeasonStatistic>> GetAllPlayerSeasonStatisticsAsync();
        Task<List<Match>> GetAllMatchesAsync();
        Task<List<Standing>> GetAllStandingsAsync();
        Task<List<MatchEvent>> GetAllMatchEventsAsync();
        Task<List<Transfer>> GetAllTransfersAsync();
        Task<List<TeamStatistic>> GetAllTeamStatisticsAsync();
    }
}
