using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetLineup
{
    public class ApiFootballLineupResponse
    {
        public List<ApiLineupWrapper> response { get; set; }
    }

    public class ApiLineupWrapper
    {
        public ApiLineupTeam team { get; set; }
        public string formation { get; set; }
    }

    public class ApiLineupTeam
    {
        public int id { get; set; }
    }
}
