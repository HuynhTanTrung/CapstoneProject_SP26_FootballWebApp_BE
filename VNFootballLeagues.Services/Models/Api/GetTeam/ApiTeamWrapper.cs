using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTeam
{
    public class ApiTeamWrapper
    {
        public ApiTeam team { get; set; }
        public ApiVenue venue { get; set; }

    }
}
