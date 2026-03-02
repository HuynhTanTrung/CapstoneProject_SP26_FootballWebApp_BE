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
        Task<List<League>> SyncLeaguesAsync();
        Task<List<PlayerWithStatsDto>> SyncPlayersByLeagueAsync(int apiLeagueId, int season);

    }
}
