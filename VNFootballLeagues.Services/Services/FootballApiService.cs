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
using VNFootballLeagues.Services.Models.Api.GetLeague;
using VNFootballLeagues.Services.Models.Api.GetPlayer;
using VNFootballLeagues.Services.Models.Api.GetTransfer;

namespace VNFootballLeagues.Services.Services
{
    public class FootballApiService : IFootballApiService
    {
        private readonly HttpClient _httpClient;
        private readonly VNFootballLeaguesDBContext _context;
        private const string ApiKey = "6eb9790bc76fca11467f05ff4386793a";
        private const string BaseUrl = "https://v3.football.api-sports.io/";
        private readonly int[] VietnamLeagueApiIds = { 340, 341, 637 };

        public FootballApiService(HttpClient httpClient, VNFootballLeaguesDBContext context)
        {
            _context = context;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("x-apisports-key", ApiKey);
        }

        private int? ParseHeight(string height)
        {
            if (string.IsNullOrEmpty(height)) return null;
            return int.TryParse(height.Replace(" cm", ""), out var h) ? h : null;
        }

        private int? ParseWeight(string weight)
        {
            if (string.IsNullOrEmpty(weight)) return null;
            return int.TryParse(weight.Replace(" kg", ""), out var w) ? w : null;
        }

        public async Task<List<Team>> SyncTeamsByLeagueAsync(int apiLeagueId, int season)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced before syncing teams.");

            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballTeamResponse>(
                    $"teams?league={apiLeagueId}&season={season}");

            if (response?.response == null || !response.response.Any())
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
                        Founded = apiTeam.founded,
                        National = apiTeam.national,
                        ClubId = 1, // adjust if you later separate Club properly
                        StadiumId = stadium?.StadiumId,
                        LeagueId = league.LeagueId
                    };

                    _context.Teams.Add(newTeam);
                }
                else
                {
                    existingTeam.TeamName = apiTeam.name;
                    existingTeam.ShortName = apiTeam.code;
                    existingTeam.LogoUrl = apiTeam.logo;
                    existingTeam.Founded = apiTeam.founded;
                    existingTeam.National = apiTeam.national;
                    existingTeam.StadiumId = stadium?.StadiumId;
                    existingTeam.LeagueId = league.LeagueId;
                }
            }

            await _context.SaveChangesAsync();

            return await _context.Teams
                .Include(t => t.League)
                .Include(t => t.Stadium)
                .Where(t => t.LeagueId == league.LeagueId)
                .ToListAsync();
        }

        public async Task<List<League>> SyncLeaguesAsync()
        {
            var leagues = new List<League>();

            foreach (var apiLeagueId in VietnamLeagueApiIds)
            {
                var response = await _httpClient
                    .GetFromJsonAsync<ApiFootballLeagueResponse>($"leagues?id={apiLeagueId}");

                if (response?.response == null || !response.response.Any())
                    continue;

                var apiLeague = response.response.First().league;

                var existing = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeague.id);

                if (existing != null)
                {
                    existing.LeagueName = apiLeague.name;
                    existing.LeagueType = apiLeague.type;
                    existing.LogoUrl = apiLeague.logo;
                    leagues.Add(existing);
                }
                else
                {
                    var league = new League
                    {
                        ApiLeagueId = apiLeague.id,
                        LeagueName = apiLeague.name,
                        LeagueType = apiLeague.type,
                        LogoUrl = apiLeague.logo
                    };

                    _context.Leagues.Add(league);
                    leagues.Add(league);
                }
            }

            await _context.SaveChangesAsync();
            return leagues;
        }

        public async Task<List<PlayerWithStatsDto>> SyncPlayersByLeagueAsync(int apiLeagueId, int season)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced first.");

            var dbLeagueId = league.LeagueId;

            var teamsExist = await _context.Teams.AnyAsync(t => t.ApiTeamId != null);
            if (!teamsExist)
                throw new Exception("Teams must be synced before syncing players.");

            var page = 1;
            var allApiPlayers = new List<ApiPlayerWrapper>();

            while (true)
            {
                var response = await _httpClient
                    .GetFromJsonAsync<ApiFootballPlayerResponse>(
                        $"players?league={apiLeagueId}&season={season}&page={page}");

                if (response?.response == null || !response.response.Any())
                    break;

                allApiPlayers.AddRange(response.response);

                if (page >= response.paging.total)
                    break;

                page++;
            }

            return await SyncPlayersInternal(allApiPlayers, season, dbLeagueId);
        }

        private async Task<List<PlayerWithStatsDto>> SyncPlayersInternal(
            List<ApiPlayerWrapper> apiPlayers,
            int seasonYear,
            int dbLeagueId)
        {
            var result = new List<PlayerWithStatsDto>();

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.Year == seasonYear);

            if (season == null)
            {
                season = new Season
                {
                    Year = seasonYear,
                    LeagueId = dbLeagueId
                };
                _context.Seasons.Add(season);
                await _context.SaveChangesAsync();
            }

            foreach (var wrapper in apiPlayers)
            {
                var apiPlayer = wrapper.player;
                var apiStats = wrapper.statistics?.FirstOrDefault();
                if (apiStats == null)
                    continue;

                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.ApiTeamId == (int?)apiStats.team.id);

                if (team == null)
                    continue;

                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayer.id);

                if (existingPlayer == null)
                {
                    existingPlayer = new Player
                    {
                        ApiPlayerId = apiPlayer.id,
                        FirstName = apiPlayer.firstname,
                        LastName = apiPlayer.lastname,
                        FullName = apiPlayer.name,
                        DateOfBirth = DateTime.TryParse(apiPlayer.birth?.date, out var dob)
                            ? DateOnly.FromDateTime(dob)
                            : null,
                        BirthPlace = apiPlayer.birth?.place,
                        BirthCountry = apiPlayer.birth?.country,
                        Age = apiPlayer.age,
                        IsInjured = apiPlayer.injured ?? false,
                        Nationality = apiPlayer.nationality,
                        HeightCm = ParseHeight(apiPlayer.height),
                        WeightKg = ParseWeight(apiPlayer.weight),
                        PhotoUrl = apiPlayer.photo,
                        TeamId = team.TeamId,
                        Position = apiPlayer.position,
                        Number = apiPlayer.number
                    };

                    _context.Players.Add(existingPlayer);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    existingPlayer.BirthPlace = apiPlayer.birth?.place;
                    existingPlayer.BirthCountry = apiPlayer.birth?.country;
                    existingPlayer.Age = apiPlayer.age;
                    existingPlayer.IsInjured = apiPlayer.injured ?? false;
                }

                var existingStat = await _context.PlayerSeasonStatistics
                    .FirstOrDefaultAsync(s =>
                        s.PlayerId == existingPlayer.PlayerId &&
                        s.SeasonId == season.SeasonId &&
                        s.LeagueId == dbLeagueId);

                if (existingStat == null)
                {
                    existingStat = new PlayerSeasonStatistic
                    {
                        PlayerId = existingPlayer.PlayerId,
                        SeasonId = season.SeasonId,
                        LeagueId = dbLeagueId,
                        TeamId = team.TeamId
                    };

                    _context.PlayerSeasonStatistics.Add(existingStat);
                }

                existingStat.Appearances = apiStats.games?.appearances ?? 0;
                existingStat.Lineups = apiStats.games?.lineups ?? 0;
                existingStat.Minutes = apiStats.games?.minutes ?? 0;
                existingStat.Rating = decimal.TryParse(apiStats.games?.rating, out var rating) ? rating : null;
                existingStat.Goals = apiStats.goals?.total ?? 0;
                existingStat.Assists = apiStats.goals?.assists ?? 0;
                existingStat.YellowCards = apiStats.cards?.yellow ?? 0;
                existingStat.RedCards = apiStats.cards?.red ?? 0;

                result.Add(new PlayerWithStatsDto
                {
                    PlayerId = existingPlayer.PlayerId,
                    FullName = existingPlayer.FullName,
                    Statistics = new List<PlayerSeasonStatDto>
            {
                new PlayerSeasonStatDto
                {
                    Season = seasonYear,
                    LeagueId = dbLeagueId,
                    TeamId = team.TeamId,
                    Appearances = existingStat.Appearances,
                    Lineups = existingStat.Lineups,
                    Minutes = existingStat.Minutes,
                    Goals = existingStat.Goals,
                    Assists = existingStat.Assists,
                    YellowCards = existingStat.YellowCards,
                    RedCards = existingStat.RedCards,
                    Rating = existingStat.Rating
                }
            }
                });
            }

            await _context.SaveChangesAsync();
            return result;
        }
    }
}
