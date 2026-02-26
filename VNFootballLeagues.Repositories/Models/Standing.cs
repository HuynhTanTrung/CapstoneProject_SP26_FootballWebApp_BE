using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Repositories.Models
{
    public partial class Standing
    {
        public int StandingId { get; set; }

        public int SeasonId { get; set; }
        public int TeamId { get; set; }

        public int Played { get; set; } = 0;
        public int Won { get; set; } = 0;
        public int Draw { get; set; } = 0;
        public int Lost { get; set; } = 0;

        public int GoalsFor { get; set; } = 0;
        public int GoalsAgainst { get; set; } = 0;
        public int GoalDifference { get; private set; }

        public int Points { get; set; } = 0;

        public int? Position { get; set; }

        public virtual Season Season { get; set; }
        public virtual Team Team { get; set; }
    }
}
