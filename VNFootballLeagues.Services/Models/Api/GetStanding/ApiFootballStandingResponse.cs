using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetStanding
{
    public class ApiFootballStandingResponse
    {
        public string get { get; set; }

        public ApiStandingParameters parameters { get; set; }

        public List<object> errors { get; set; }

        public int results { get; set; }

        public ApiStandingPaging paging { get; set; }

        public List<ApiStandingWrapper> response { get; set; }
    }

    public class ApiStandingWrapper
    {
        public ApiStandingLeague league { get; set; }
    }

    public class ApiStandingLeague
    {
        public int id { get; set; }

        public string name { get; set; }

        public string country { get; set; }

        public string logo { get; set; }

        public string flag { get; set; }

        public int season { get; set; }

        public List<List<ApiStanding>> standings { get; set; }
    }

    public class ApiStanding
    {
        public int rank { get; set; }

        public ApiStandingTeam team { get; set; }

        public int points { get; set; }

        public int goalsDiff { get; set; }

        public string group { get; set; }

        public string form { get; set; }

        public string status { get; set; }

        public string description { get; set; }

        public ApiStandingStats all { get; set; }

        public ApiStandingStats home { get; set; }

        public ApiStandingStats away { get; set; }

        public DateTime update { get; set; }
    }

    public class ApiStandingTeam
    {
        public int id { get; set; }

        public string name { get; set; }

        public string logo { get; set; }
    }

    public class ApiStandingStats
    {
        public int played { get; set; }

        public int win { get; set; }

        public int draw { get; set; }

        public int lose { get; set; }

        public ApiStandingGoals goals { get; set; }
    }

    public class ApiStandingGoals
    {
        [JsonPropertyName("for")]
        public int forValue { get; set; }

        public int against { get; set; }
    }

    public class ApiStandingParameters
    {
        public string league { get; set; }

        public string season { get; set; }
    }

    public class ApiStandingPaging
    {
        public int current { get; set; }

        public int total { get; set; }
    }
}
