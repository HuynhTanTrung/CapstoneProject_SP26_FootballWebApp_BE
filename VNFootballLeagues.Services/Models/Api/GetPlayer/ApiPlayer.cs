using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class ApiPlayer
    {
        public int id { get; set; }
        public string name { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public int? age { get; set; }
        public string nationality { get; set; }
        public string height { get; set; }
        public string weight { get; set; }
        public bool? injured { get; set; }
        public string photo { get; set; }
        public string? position { get; set; }
        public int? number { get; set; }
        public ApiBirth birth { get; set; }
    }

    public class ApiBirth
    {
        public string? date { get; set; }
        public string place { get; set; }
        public string country { get; set; }
    }
}
