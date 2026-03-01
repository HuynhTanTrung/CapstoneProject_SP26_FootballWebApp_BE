using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class PlayerDto
    {
        public int PlayerId { get; set; }
        public string FullName { get; set; }
        public string Nationality { get; set; }
        public int? HeightCm { get; set; }
        public int? WeightKg { get; set; }
        public string PhotoUrl { get; set; }
    }
}
