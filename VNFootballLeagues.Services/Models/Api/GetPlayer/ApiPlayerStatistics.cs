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

        public ApiStatSubstitutes substitutes { get; set; }

        public ApiStatShots shots { get; set; }
        public ApiStatGoals goals { get; set; }
        public ApiStatPasses passes { get; set; }

        public ApiStatDribbles dribbles { get; set; }
        public ApiStatDuels duels { get; set; }

        public ApiStatTackles tackles { get; set; }

        public ApiStatFouls fouls { get; set; }

        public ApiStatCards cards { get; set; }

        public ApiStatPenalty penalty { get; set; }
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

        public bool? captain { get; set; }
    }

    public class ApiStatSubstitutes
    {
        public int? @in { get; set; }
        public int? @out { get; set; }
        public int? bench { get; set; }
    }

    public class ApiStatShots
    {
        public int? total { get; set; }
        public int? on { get; set; }
    }

    public class ApiStatGoals
    {
        public int? total { get; set; }
        public int? conceded { get; set; }
        public int? assists { get; set; }
        public int? saves { get; set; }
    }

    public class ApiStatPasses
    {
        public int? total { get; set; }
        public int? key { get; set; }

        public string accuracy { get; set; }
    }

    public class ApiStatDribbles
    {
        public int? attempts { get; set; }
        public int? success { get; set; }
        public int? past { get; set; }
    }

    public class ApiStatDuels
    {
        public int? total { get; set; }
        public int? won { get; set; }
    }

    public class ApiStatTackles
    {
        public int? total { get; set; }
        public int? blocks { get; set; }
        public int? interceptions { get; set; }
    }

    public class ApiStatFouls
    {
        public int? drawn { get; set; }
        public int? committed { get; set; }
    }

    public class ApiStatCards
    {
        public int? yellow { get; set; }
        public int? yellowred { get; set; }
        public int? red { get; set; }
    }

    public class ApiStatPenalty
    {
        public int? won { get; set; }
        public int? commited { get; set; }
        public int? scored { get; set; }
        public int? missed { get; set; }
        public int? saved { get; set; }
    }
}