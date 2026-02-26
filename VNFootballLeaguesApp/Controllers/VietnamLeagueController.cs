using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FootballController : ControllerBase
    {
        private readonly IFootballApiService _service;

        public FootballController(IFootballApiService service)
        {
            _service = service;
        }

        [HttpGet("vietnam-teams")]
        public async Task<IActionResult> GetVietnamTeams(int season)
        {
            var data = await _service.GetVietnamTeamsAsync(season);
            return Ok(data);
        }
    }
}
