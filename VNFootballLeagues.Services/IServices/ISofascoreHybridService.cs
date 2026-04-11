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
        Task<List<SeasonListItemDto>> GetAllSeasonsAsync(int? leagueId = null, int? tournamentId = null);
        Task<List<Team>> GetAllTeamsAsync();
        Task<List<Match>> GetAllMatchesAsync(int? tournamentId = null, int? seasonId = null);
        Task<List<MatchStatistic>> GetAllMatchStatisticsAsync();
        Task<List<Player>> GetAllPlayersAsync(int? teamId = null, int? sofascoreTeamId = null);
        Task<List<Player>> GetAllTeamPlayersByLeagueSeasonAsync(int tournamentId, int seasonId);
        Task<List<PlayerSeasonStatistic>> GetAllPlayerSeasonStatisticsAsync();
        Task<object> AggregateSeasonStatsFromMatchStatsAsync(int? leagueId = null, int? seasonId = null, int? playerId = null);
        Task<List<MatchEvent>> GetAllMatchEventsAsync();
        Task<List<Standing>> GetAllStandingsAsync(int tournamentId, int seasonId);
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByApiFixtureIdAsync(int apiFixtureId, bool fetchIfEmpty = false);
        Task<bool> MatchExistsByApiFixtureIdAsync(int apiFixtureId);
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByLeagueSeasonAsync(int tournamentId, int seasonId);
        Task<List<Lineup>> GetAllLineupsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> SyncMatchesByRoundAsync(int tournamentId, int seasonId);
        Task<object> SyncMatchStatisticsAsync(int apiFixtureId);
        Task<object> SyncMatchStatisticsByLeagueAndSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> GetTeamsByTournamentAsync(int tournamentId);
        Task<object> SyncTeamsFromStandingsAsync(int tournamentId, int seasonId);
        Task<object> SyncTeamPlayersAsync(int sofascoreTeamId);
        Task<object> SyncAllTeamPlayersAsync(int tournamentId, int seasonId);
        Task<object> SyncAllPlayerStatisticsAsync(int tournamentId, int seasonId);
        Task<object> SyncPlayerStatsByPlayerIdAsync(int playerId);
        Task<object> SyncVietnameseLeaguesAsync();
        Task<object> SyncSeasonsByLeagueAsync(int apiTournamentId);
        Task<object> SyncStandingsAsync(int apiTournamentId, int apiSeasonId);
        Task<object> SyncMatchEventsAsync(int apiFixtureId);
        Task<object> FetchPlayerMatchStatsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> FetchPlayerMatchStatsByRoundAsync(int apiTournamentId, int apiSeasonId, string round);
        Task<object> FetchPlayerMatchStatsByApiMatchIdAsync(int apiFixtureId);
        Task<object> SyncMatchLineupsAsync(int apiFixtureId);
        Task<object> FetchLineupsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> SyncTeamContractsAsync(int apiTeamId);
        Task<object> SyncTeamTransfersAsync(int apiTeamId);
        Task<object> SyncAllTeamContractsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> SyncAllTeamTransfersByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> GetContractsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId);
        Task<object> GetAllTransfersAsync();
        Task<List<Player>> GetAllPlayersAsync();
        Task<List<Match>> GetTeamLastMatchesFromDbAsync(int apiTeamId, int count);
        Task<List<Team>> GetTeamsByIdsAsync(List<int> teamIds);
        Task<object> AddFavoritePlayerAsync(Guid userId, int apiPlayerId);
        Task<object> RemoveFavoritePlayerAsync(Guid userId, int apiPlayerId);
        Task<object> GetAllFavoritesAsync();
        Task<object> GetFavoriteByUserAsync(Guid userId);
    }
}
