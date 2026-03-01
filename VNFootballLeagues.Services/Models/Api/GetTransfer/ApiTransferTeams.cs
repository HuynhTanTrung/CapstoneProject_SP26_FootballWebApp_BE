using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTransfer
{
    public class ApiTransferTeams
    {
        public ApiTransferTeam @in { get; set; }
        public ApiTransferTeam @out { get; set; }
    }
}
