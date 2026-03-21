using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices
{
    public interface ISofascoreHybridService
    {
        Task<object> SyncMatchesByRoundAsync(int tournamentId, int seasonId);
        Task<object> SyncMatchStatisticsAsync(int apiFixtureId);
        Task<object> GetTeamsByTournamentAsync(int tournamentId);
        Task<object> SyncTeamsFromStandingsAsync(int tournamentId, int seasonId);
    }
}
