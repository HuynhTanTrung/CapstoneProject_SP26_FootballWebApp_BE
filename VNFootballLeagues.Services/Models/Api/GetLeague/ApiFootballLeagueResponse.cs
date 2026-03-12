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

    public class ApiLeague
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public string logo { get; set; } = "";
    }

    public class ApiSeason
    {
        public int year { get; set; }
        public string start { get; set; } = "";
        public string end { get; set; } = "";
        public bool current { get; set; }
        public object coverage { get; set; }
    }

    public class ApiLeagueWrapper
    {
        public ApiLeague league { get; set; } = new();
        public List<ApiSeason> seasons { get; set; } = new();
    }
}
