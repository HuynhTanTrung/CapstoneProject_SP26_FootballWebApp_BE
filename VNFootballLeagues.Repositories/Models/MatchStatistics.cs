using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Repositories.Models
{
    public partial class MatchStatistics
    {
        public int MatchStatisticsId { get; set; }

        public int MatchId { get; set; }
        public int TeamId { get; set; }

        public int? Possession { get; set; }
        public int? Shots { get; set; }
        public int? ShotsOnTarget { get; set; }
        public int? Corners { get; set; }
        public int? Fouls { get; set; }
        public int? YellowCards { get; set; }
        public int? RedCards { get; set; }
        public int? Offsides { get; set; }

        public virtual Match Match { get; set; }
        public virtual Team Team { get; set; }
    }
}
