using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices
{
    public interface IFootballApiService
    {
        Task<List<Team>> GetVietnamTeamsAsync(int season);
    }
}
