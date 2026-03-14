using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTransfer
{
    public class ApiFootballTransferResponse
    {
        public List<TransferResponse> response { get; set; }
    }

    public class TransferResponse
    {
        public PlayerInfo player { get; set; }
        public List<TransferDetail> transfers { get; set; }
    }

    public class PlayerInfo
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class TransferDetail
    {
        public string date { get; set; }
        public string type { get; set; }
        public TransferTeams teams { get; set; }
    }

    public class TransferTeams
    {
        public TransferTeamDetail @in { get; set; }
        public TransferTeamDetail @out { get; set; }
    }

    public class TransferTeamDetail
    {
        public int? id { get; set; }
        public string name { get; set; }
        public string logo { get; set; }
    }
}
