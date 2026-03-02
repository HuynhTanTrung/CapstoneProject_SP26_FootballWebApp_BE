using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetLeague
{
    public class ApiLeague
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public string logo { get; set; } = "";
    }
}
