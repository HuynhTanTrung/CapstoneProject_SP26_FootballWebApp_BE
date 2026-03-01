using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Services.Models.Api.GetPlayer
{
    public class ApiPlayerWrapper
    {
        public ApiPlayer player { get; set; }
        public List<ApiPlayerStatistics> statistics { get; set; }
    }
}
