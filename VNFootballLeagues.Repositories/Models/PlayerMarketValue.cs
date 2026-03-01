using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Repositories.Models
{
    public class PlayerMarketValue
    {
        public int PlayerMarketValueId { get; set; }
        public int PlayerId { get; set; }
        public decimal MarketValue { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
