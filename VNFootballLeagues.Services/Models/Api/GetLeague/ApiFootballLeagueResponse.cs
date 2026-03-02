using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetLeague
{
    public class ApiFootballLeagueResponse
    {
        public List<ApiLeagueWrapper> response { get; set; } = new();
    }
}
