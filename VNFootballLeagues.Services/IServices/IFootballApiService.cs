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
        Task<List<Team>> GetVietnamTeamsAsync(int season);
        Task<List<PlayerWithStatsDto>> SyncPlayersByTeamAsync(int teamApiId, int season);
        Task<List<PlayerWithStatsDto>> SyncPlayersByLeagueAsync(int season);
        Task SyncPlayerTransfersAsync(Player player);
    }
}
