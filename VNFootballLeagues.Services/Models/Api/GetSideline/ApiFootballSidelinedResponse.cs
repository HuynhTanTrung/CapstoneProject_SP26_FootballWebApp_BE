using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetSideline
{
    public class ApiFootballSidelinedResponse
    {
        public List<SidelinedResponse> response { get; set; }
    }

    public class SidelinedResponse
    {
        public int? player { get; set; }
        public int? team { get; set; }
        public string type { get; set; }
        public string start { get; set; }
        public string end { get; set; }
    }
}
