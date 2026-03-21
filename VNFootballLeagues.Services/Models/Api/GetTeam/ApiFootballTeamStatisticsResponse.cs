using Microsoft.Azure.Documents.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTeam
{
    public class ApiFootballTeamStatisticsResponse
    {
        public TeamStatisticsResponse response { get; set; }
    }

    public class TeamStatisticsResponse
    {
        public LeagueInfo league { get; set; }
        public TeamInfo team { get; set; }
        public string form { get; set; }
        public Fixtures fixtures { get; set; }
        public Goals goals { get; set; }
        public Biggest biggest { get; set; }
        public CleanSheet clean_sheet { get; set; }
        public FailedToScore failed_to_score { get; set; }
        public Penalty penalty { get; set; }
        public Cards cards { get; set; }
    }

    public class LeagueInfo
    {
        public int id { get; set; }
        public string name { get; set; }
        public string country { get; set; }
        public string logo { get; set; }
        public string flag { get; set; }
        public int season { get; set; }
    }

    public class TeamInfo
    {
        public int id { get; set; }
        public string name { get; set; }
        public string logo { get; set; }
    }

    public class Fixtures
    {
        public FixtureDetail played { get; set; }
        public FixtureDetail wins { get; set; }
        public FixtureDetail draws { get; set; }
        public FixtureDetail loses { get; set; }
    }

    public class FixtureDetail
    {
        public int home { get; set; }
        public int away { get; set; }
        public int total { get; set; }
    }

    public class Goals
    {
        public GoalDetail @for { get; set; }
        public GoalDetail against { get; set; }
    }

    public class GoalDetail
    {
        public GoalTotal total { get; set; }
        public GoalAverage average { get; set; }
        public Dictionary<string, MinuteDetail> minute { get; set; }
        public Dictionary<string, UnderOverDetail> under_over { get; set; }
    }

    public class GoalTotal
    {
        public int home { get; set; }
        public int away { get; set; }
        public int total { get; set; }
    }

    public class GoalAverage
    {
        public string home { get; set; }
        public string away { get; set; }
        public string total { get; set; }
    }

    public class MinuteDetail
    {
        public int? total { get; set; }
        public string percentage { get; set; }
    }

    public class UnderOverDetail
    {
        public int over { get; set; }
        public int under { get; set; }
    }

    public class Biggest
    {
        public Streak streak { get; set; }
        public BiggestWins wins { get; set; }
        public BiggestLoses loses { get; set; }
        public BiggestGoals goals { get; set; }
    }

    public class Streak
    {
        public int wins { get; set; }
        public int draws { get; set; }
        public int loses { get; set; }
    }

    public class BiggestWins
    {
        public string home { get; set; }
        public string away { get; set; }
    }

    public class BiggestLoses
    {
        public string home { get; set; }
        public string away { get; set; }
    }

    public class BiggestGoals
    {
        public BiggestGoalDetail @for { get; set; }
        public BiggestGoalDetail against { get; set; }
    }

    public class BiggestGoalDetail
    {
        public int home { get; set; }
        public int away { get; set; }
    }

    public class CleanSheet
    {
        public int home { get; set; }
        public int away { get; set; }
        public int total { get; set; }
    }

    public class FailedToScore
    {
        public int home { get; set; }
        public int away { get; set; }
        public int total { get; set; }
    }

    public class Penalty
    {
        public PenaltyDetail scored { get; set; }
        public PenaltyDetail missed { get; set; }
        public int total { get; set; }
    }

    public class PenaltyDetail
    {
        public int total { get; set; }
        public string percentage { get; set; }
    }

    public class Cards
    {
        public Dictionary<string, MinuteDetail> yellow { get; set; }
        public Dictionary<string, MinuteDetail> red { get; set; }
    }
}