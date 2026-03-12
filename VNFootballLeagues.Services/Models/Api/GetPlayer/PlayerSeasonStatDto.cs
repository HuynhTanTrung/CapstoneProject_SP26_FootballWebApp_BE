using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class PlayerSeasonStatDto
    {
        public int Season { get; set; }

        public int LeagueId { get; set; }

        public int TeamId { get; set; }

        public int? Appearances { get; set; }

        public int? Lineups { get; set; }

        public int? Minutes { get; set; }

        public int? SubstitutionsIn { get; set; }

        public int? SubstitutionsOut { get; set; }

        public int? Goals { get; set; }

        public int? Assists { get; set; }

        public int? ShotsTotal { get; set; }

        public int? ShotsOnTarget { get; set; }

        public int? PassesTotal { get; set; }

        public int? PassesKey { get; set; }

        public decimal? PassesAccuracy { get; set; }

        public int? DribblesAttempted { get; set; }

        public int? DribblesSuccess { get; set; }

        public decimal? DribblesSuccessRate { get; set; }
        public int? DuelsWon { get; set; }

        public int? DuelsTotal { get; set; }

        public decimal? DuelsWonRate { get; set; }

        public int? Tackles { get; set; }

        public int? Interceptions { get; set; }

        public int? FoulsDrawn { get; set; }

        public int? FoulsCommitted { get; set; }

        public int? YellowCards { get; set; }

        public int? RedCards { get; set; }

        public int? PenaltiesScored { get; set; }

        public int? PenaltiesMissed { get; set; }

        public decimal? Rating { get; set; }
    }
}
