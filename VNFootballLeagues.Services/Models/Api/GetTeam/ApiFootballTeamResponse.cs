using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Services.Models.Api.GetTeam;

namespace VNFootballLeagues.Services.Models.Api
{
    public class ApiFootballTeamResponse
    {
        public List<ApiTeamWrapper> response { get; set; }
    }
}
