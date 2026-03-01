using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetTransfer
{
    public class ApiTransferWrapper
    {
        public ApiTransferPlayer player { get; set; }
        public List<ApiTransfer> transfers { get; set; }
    }
}
