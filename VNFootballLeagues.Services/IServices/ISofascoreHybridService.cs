using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;

namespace VNFootballLeagues.Services.IServices
{
    public interface ISofascoreHybridService
    {
        Task<List<League>> GetAllLeaguesAsync();
        Task<List<SeasonListItemDto>> GetAllSeasonsAsync();
        Task<List<Team>> GetAllTeamsAsync();
        Task<List<Match>> GetAllMatchesAsync(int? tournamentId = null, int? seasonId = null);
        Task<List<MatchStatistic>> GetAllMatchStatisticsAsync();
        Task<List<Player>> GetAllPlayersAsync(int sofascoreTeamId);
        Task<List<Player>> GetAllTeamPlayersByLeagueSeasonAsync(int tournamentId, int seasonId);
        Task<List<PlayerSeasonStatistic>> GetAllPlayerSeasonStatisticsAsync();
        Task<List<MatchEvent>> GetAllMatchEventsAsync(int apiFixtureId);
        Task<List<Standing>> GetAllStandingsAsync(int tournamentId, int seasonId);
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByApiFixtureIdAsync(int apiFixtureId);
        Task<bool> MatchExistsByApiFixtureIdAsync(int apiFixtureId);
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByLeagueSeasonAsync(int tournamentId, int seasonId);
        Task<List<MatchStatistic>> GetMatchStatisticsByMatchAsync(int apiFixtureId);
        Task<object> SyncMatchesByRoundAsync(int tournamentId, int seasonId);
        Task<object> SyncMatchStatisticsAsync(int apiFixtureId);
        Task<object> GetTeamsByTournamentAsync(int tournamentId);
        Task<object> SyncTeamsFromStandingsAsync(int tournamentId, int seasonId);
        Task<object> SyncTeamPlayersAsync(int sofascoreTeamId);
        Task<object> SyncAllTeamPlayersAsync(int tournamentId, int seasonId);
        Task<object> SyncAllPlayerStatisticsAsync(int tournamentId, int seasonId);
        Task<object> SyncVietnameseLeaguesAsync();
        Task<object> SyncSeasonsByLeagueAsync(int apiTournamentId);
        Task<object> SyncStandingsAsync(int apiTournamentId, int apiSeasonId);
        Task<object> SyncMatchEventsAsync(int apiFixtureId);
        Task<object> FetchPlayerMatchStatsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> FetchPlayerMatchStatsByApiMatchIdAsync(int apiFixtureId);
        Task<object> SyncMatchStatisticsByLeagueAndSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<List<Match>> GetTeamLastMatchesFromDbAsync(int apiTeamId, int count);
    }
}
