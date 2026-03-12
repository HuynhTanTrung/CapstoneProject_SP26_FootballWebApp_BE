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

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Nationality { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public int? Age { get; set; }

        public decimal? HeightCm { get; set; }

        public decimal? WeightKg { get; set; }

        public string Position { get; set; }

        public int? Number { get; set; }

        public bool? IsInjured { get; set; }

        public string PhotoUrl { get; set; }

        public int? TeamId { get; set; }

        public List<PlayerSeasonStatDto> Statistics { get; set; } = new();
    }
}
