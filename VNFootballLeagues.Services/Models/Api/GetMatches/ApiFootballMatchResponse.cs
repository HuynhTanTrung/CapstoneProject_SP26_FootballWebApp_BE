using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetMatches
{
    public class ApiFootballMatchResponse
    {
        public List<ApiMatchWrapper> response { get; set; }
    }

    public class ApiMatchWrapper
    {
        public ApiMatch fixture { get; set; }
        public ApiMatchLeague league { get; set; }
        public ApiMatchTeams teams { get; set; }
        public ApiMatchGoals goals { get; set; }
    }

    public class ApiMatch
    {
        public int id { get; set; }
        public string referee { get; set; }
        public string timezone { get; set; }
        public string date { get; set; }
        public long timestamp { get; set; }
        public int? attendance { get; set; }
        public ApiMatchVenue venue { get; set; }
        public ApiMatchStatus status { get; set; }
        public ApiMatchPeriods periods { get; set; }
    }

    public class ApiMatchVenue
    {
        public int? id { get; set; }
        public string name { get; set; }
        public string city { get; set; }
    }

    public class ApiMatchStatus
    {
        public string @long { get; set; }
        public string shortStatus { get; set; }
        public int? elapsed { get; set; }
    }

    public class ApiMatchPeriods
    {
        public int? first { get; set; }
        public int? second { get; set; }
    }

    public class ApiMatchLeague
    {
        public int id { get; set; }
        public string round { get; set; }
        public int season { get; set; }
    }

    public class ApiMatchTeams
    {
        public ApiMatchTeam home { get; set; }
        public ApiMatchTeam away { get; set; }
    }

    public class ApiMatchTeam
    {
        public int id { get; set; }
    }

    public class ApiMatchGoals
    {
        public int? home { get; set; }
        public int? away { get; set; }
    }
}