using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTransfer
{
    public class ApiTransfer
    {
        public DateTime? date { get; set; }
        public string type { get; set; }
        public ApiTransferTeams teams { get; set; }
    }
}
