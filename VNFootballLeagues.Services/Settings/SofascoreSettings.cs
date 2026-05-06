using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Settings
{
    public class SofascoreSettings
    {
        public string BaseUrl { get; set; } = "https://www.sofascore.com";
        public string ApiBaseUrl { get; set; } = "https://api.sofascore.com/api/v1";
        public string ImageBaseUrl { get; set; } = "https://images.sofascore.com";
        public string ScraperApiKey { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
        public int RequestDelayMs { get; set; } = 500;

        public EndpointConfig Endpoints { get; set; } = new();

        public SelectorConfig Selectors { get; set; } = new();
    }

    public class EndpointConfig
    {
        public string Rounds { get; set; } = "/unique-tournament/{tournamentId}/season/{seasonId}/rounds";
        public string EventsByRound { get; set; } = "/unique-tournament/{tournamentId}/season/{seasonId}/events/round/{round}";
        public string EventsLast { get; set; } = "/unique-tournament/{tournamentId}/season/{seasonId}/events/last/{page}";
        public string EventsNext { get; set; } = "/unique-tournament/{tournamentId}/season/{seasonId}/events/next/{page}";
        public string Standings { get; set; } = "/unique-tournament/{tournamentId}/season/{seasonId}/standings/total";
        public string Seasons { get; set; } = "/unique-tournament/{tournamentId}/seasons";
        public string Statistics { get; set; } = "/event/{fixtureId}/statistics";
        public string Incidents { get; set; } = "/event/{fixtureId}/incidents";
        public string Lineups { get; set; } = "/event/{fixtureId}/lineups";
        public string Shotmap { get; set; } = "/event/{fixtureId}/shotmap";
        public string TeamPlayers { get; set; } = "/team/{teamId}/players";
        public string TeamDetail { get; set; } = "/team/{teamId}";
        public string PlayerStats { get; set; } = "/player/{playerId}/unique-tournament/{tournamentId}/season/{seasonId}/statistics/overall";
        public string PlayerMatchStats { get; set; } = "/event/{fixtureId}/player/{playerId}/statistics";
        public string TransferHistory { get; set; } = "/player/{playerId}/transfer-history";
        public string UniqueTournament { get; set; } = "/unique-tournament/{tournamentId}";
    }

    public class SelectorConfig
    {
        public string HomeScore { get; set; } = "[data-testid='home-score']";
        public string AwayScore { get; set; } = "[data-testid='away-score']";
        public string MatchStatus { get; set; } = "[data-testid='match-status']";
        public string EventTime { get; set; } = "[data-testid='event-time']";
    }
}
