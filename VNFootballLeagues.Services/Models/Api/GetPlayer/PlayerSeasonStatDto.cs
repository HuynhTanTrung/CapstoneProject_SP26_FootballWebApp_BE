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

        public int? Goals { get; set; }
        public int? Assists { get; set; }

        public int? YellowCards { get; set; }
        public int? RedCards { get; set; }

        public decimal? Rating { get; set; }
    }
}
