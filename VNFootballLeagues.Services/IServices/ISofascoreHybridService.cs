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
        // ==================== GetAll (đọc từ DB) ====================
        Task<List<League>> GetAllLeaguesAsync();
        /// <param name="leagueId">Khóa nội bộ League.LeagueId.</param>
        /// <param name="tournamentId">ApiLeagueId SofaScore (unique tournament). Nếu DB chưa có mùa, gọi API SofaScore.</param>
        Task<List<SeasonListItemDto>> GetAllSeasonsAsync(int? leagueId = null, int? tournamentId = null);
        Task<List<Team>> GetAllTeamsAsync();
        Task<List<Match>> GetAllMatchesAsync(int? tournamentId = null, int? seasonId = null);
        Task<List<MatchStatistic>> GetAllMatchStatisticsAsync();
        /// <param name="teamId">Khóa nội bộ Team.TeamId.</param>
        /// <param name="sofascoreTeamId">ApiTeamId SofaScore (giống POST sync-team-players).</param>
        Task<List<Player>> GetAllPlayersAsync(int? teamId = null, int? sofascoreTeamId = null);
        /// <summary>Cầu thủ các đội trong giải/mùa (BXH → trận đấu → toàn bộ đội giải nếu vẫn rỗng).</summary>
        Task<List<Player>> GetAllTeamPlayersByLeagueSeasonAsync(int tournamentId, int seasonId);
        Task<List<PlayerSeasonStatistic>> GetAllPlayerSeasonStatisticsAsync();
        Task<List<MatchEvent>> GetAllMatchEventsAsync();
        /// <summary>tournamentId / seasonId là ApiLeagueId / ApiSeasonId (SofaScore).</summary>
        Task<List<Standing>> GetAllStandingsAsync(int tournamentId, int seasonId);
        /// <param name="fetchIfEmpty">true: nếu DB chưa có stats thì gọi SofaScore (giống POST fetch-player-match-stats-by-match) rồi đọc lại.</param>
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByApiFixtureIdAsync(int apiFixtureId, bool fetchIfEmpty = false);
        Task<bool> MatchExistsByApiFixtureIdAsync(int apiFixtureId);
        Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByLeagueSeasonAsync(int tournamentId, int seasonId);

        Task<object> SyncMatchesByRoundAsync(int tournamentId, int seasonId);
        Task<object> SyncMatchStatisticsAsync(int apiFixtureId);
        Task<object> SyncMatchStatisticsByLeagueAndSeasonAsync(int apiTournamentId, int apiSeasonId);
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
        Task<List<Match>> GetTeamLastMatchesFromDbAsync(int apiTeamId, int count);
        Task<List<Team>> GetTeamsByIdsAsync(List<int> teamIds);
    }
}
