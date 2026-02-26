using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Api;

namespace VNFootballLeagues.Services.Services
{
    public class FootballApiService : IFootballApiService
    {
        private readonly HttpClient _httpClient;
        private readonly VNFootballLeaguesDBContext _context;
        private const string ApiKey = "6eb9790bc76fca11467f05ff4386793a";
        private const string BaseUrl = "https://v3.football.api-sports.io/";
        private const int LeagueId = 340;

        public FootballApiService(HttpClient httpClient, VNFootballLeaguesDBContext context)
        {
            _context = context;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("x-apisports-key", ApiKey);
        }

        public async Task<List<Team>> GetVietnamTeamsAsync(int season)
        {
            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballTeamResponse>(
                    $"teams?league={LeagueId}&season={season}");

            if (response?.response == null)
                return new List<Team>();

            foreach (var item in response.response)
            {
                var apiTeam = item.team;
                var apiVenue = item.venue;

                Stadium? stadium = null;

                if (apiVenue != null)
                {
                    stadium = await _context.Stadiums
                        .FirstOrDefaultAsync(s => s.ApiVenueId == apiVenue.id);

                    if (stadium == null)
                    {
                        stadium = new Stadium
                        {
                            ApiVenueId = apiVenue.id,
                            StadiumName = apiVenue.name,
                            City = apiVenue.city,
                            Capacity = apiVenue.capacity,
                            Address = apiVenue.address,
                            Surface = apiVenue.surface,
                            ImageUrl = apiVenue.image
                        };

                        _context.Stadiums.Add(stadium);
                        await _context.SaveChangesAsync();
                    }
                }

                var existingTeam = await _context.Teams
                    .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeam.id);

                if (existingTeam == null)
                {
                    var newTeam = new Team
                    {
                        ApiTeamId = apiTeam.id,
                        TeamName = apiTeam.name,
                        ShortName = apiTeam.code,
                        LogoUrl = apiTeam.logo,
                        CoachName = null,
                        ClubId = 1,
                        StadiumId = stadium?.StadiumId
                    };

                    _context.Teams.Add(newTeam);
                }
                else
                {
                    existingTeam.TeamName = apiTeam.name;
                    existingTeam.ShortName = apiTeam.code;
                    existingTeam.LogoUrl = apiTeam.logo;
                    existingTeam.StadiumId = stadium?.StadiumId;
                }
            }

            await _context.SaveChangesAsync();

            return await _context.Teams
                .Include(t => t.Stadium)
                .Where(t => t.ApiTeamId != null)
                .ToListAsync();
        }
    }
}
