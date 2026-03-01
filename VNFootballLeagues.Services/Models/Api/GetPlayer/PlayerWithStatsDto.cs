using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class PlayerWithStatsDto
    {
        public int PlayerId { get; set; }
        public string FullName { get; set; }
        public string Nationality { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public string PhotoUrl { get; set; }

        public List<PlayerSeasonStatDto> Statistics { get; set; }
    }
}
