using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Repositories.Models
{
    public class PlayerMatchStatistic
    {
        public int PlayerMatchStatisticId { get; set; }

        public int PlayerId { get; set; }
        public int MatchId { get; set; }

        public int? MinutesPlayed { get; set; }
        public int? Goals { get; set; }
        public int? Assists { get; set; }
        public decimal? Rating { get; set; }

        public virtual Player Player { get; set; }
    }
}
