using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetEvent
{
    public class ApiFootballEventResponse
    {
        public List<ApiEventWrapper> response { get; set; }
    }

    public class ApiEventWrapper
    {
        public ApiEventTime time { get; set; }
        public ApiEventTeam team { get; set; }
        public ApiEventPlayer player { get; set; }
        public ApiEventPlayer assist { get; set; }

        public string type { get; set; }
        public string detail { get; set; }
        public string comments { get; set; }
    }

    public class ApiEventTime
    {
        public int? elapsed { get; set; }
        public int? extra { get; set; }
    }

    public class ApiEventTeam
    {
        public int id { get; set; }
    }

    public class ApiEventPlayer
    {
        public int? id { get; set; }
    }
}
