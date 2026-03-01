using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTeam
{
    public class ApiVenue
    {
        public int id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string city { get; set; }
        public int? capacity { get; set; }
        public string surface { get; set; }
        public string image { get; set; }
    }
}
