using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class ApiPlayerStatistics
    {
        public ApiStatTeam team { get; set; }
        public ApiStatLeague league { get; set; }
        public ApiStatGames games { get; set; }
        public ApiStatGoals goals { get; set; }
        public ApiStatCards cards { get; set; }
    }

    public class ApiStatTeam
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class ApiStatLeague
    {
        public int id { get; set; }
        public string name { get; set; }
        public int season { get; set; }
    }

    public class ApiStatGames
    {
        public int? appearances { get; set; }
        public int? lineups { get; set; }
        public int? minutes { get; set; }
        public string position { get; set; }
        public int? number { get; set; }
        public string rating { get; set; }
    }

    public class ApiStatGoals
    {
        public int? total { get; set; }
        public int? assists { get; set; }
    }

    public class ApiStatCards
    {
        public int? yellow { get; set; }
        public int? red { get; set; }
    }
}
