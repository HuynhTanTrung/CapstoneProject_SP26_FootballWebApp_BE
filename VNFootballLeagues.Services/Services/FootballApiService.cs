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
        private const int LeagueId = 340;

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

        public async Task<List<PlayerWithStatsDto>> SyncPlayersByTeamAsync(int teamApiId, int season)
        {
            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballPlayerResponse>(
                    $"players?team={teamApiId}&season={season}");

            if (response?.response == null || !response.response.Any())
                return new List<PlayerWithStatsDto>();

            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.ApiTeamId == teamApiId);

            if (team == null)
                return new List<PlayerWithStatsDto>();

            var apiPlayerIds = response.response
                .Select(r => r.player.id)
                .ToList();

            var existingPlayers = await _context.Players
                .Where(p => apiPlayerIds.Contains(p.ApiPlayerId))
                .ToDictionaryAsync(p => p.ApiPlayerId);

            var syncedPlayers = new List<Player>();

            foreach (var item in response.response)
            {
                var apiPlayer = item.player;

                if (!existingPlayers.TryGetValue(apiPlayer.id, out var player))
                {
                    player = new Player
                    {
                        ApiPlayerId = apiPlayer.id
                    };

                    _context.Players.Add(player);
                }

                player.FirstName = apiPlayer.firstname;
                player.LastName = apiPlayer.lastname;
                player.FullName = apiPlayer.name;
                player.Nationality = apiPlayer.nationality;
                player.PhotoUrl = apiPlayer.photo;
                player.HeightCm = ParseHeight(apiPlayer.height);
                player.WeightKg = ParseWeight(apiPlayer.weight);
                player.BirthPlace = apiPlayer.birth?.place;
                player.BirthCountry = apiPlayer.birth?.country;
                player.TeamId = team.TeamId;
                player.Age = apiPlayer.age;
                player.IsInjured = apiPlayer.injured;

                player.DateOfBirth = apiPlayer.birth?.date != null
                    ? DateOnly.FromDateTime(apiPlayer.birth.date.Value)
                    : null;

                syncedPlayers.Add(player);
            }

            foreach (var entry in _context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    Console.WriteLine($"Entity: {entry.Entity.GetType().Name}");

                    foreach (var prop in entry.Properties)
                    {
                        Console.WriteLine($"  {prop.Metadata.Name} = {prop.CurrentValue}");
                    }
                }
            }

            await _context.SaveChangesAsync();

            var playerIds = syncedPlayers.Select(p => p.PlayerId).ToList();

            var existingStats = await _context.PlayerSeasonStatistics
                .Where(s => playerIds.Contains(s.PlayerId))
                .ToListAsync();

            var teamsDict = await _context.Teams
                .ToDictionaryAsync(t => t.ApiTeamId!.Value, t => t.TeamId);

            foreach (var item in response.response)
            {
                var apiPlayer = item.player;
                var player = syncedPlayers.First(p => p.ApiPlayerId == apiPlayer.id);

                if (item.statistics == null)
                    continue;

                foreach (var stat in item.statistics)
                {
                    if (!teamsDict.TryGetValue(stat.team.id, out var localTeamId))
                        continue;

                    var seasonStat = existingStats.FirstOrDefault(s =>
                        s.PlayerId == player.PlayerId &&
                        s.Season == stat.league.season &&
                        s.LeagueId == 1 &&  //PLEASE FIX THIS LATER
                        s.TeamId == localTeamId);

                    if (seasonStat == null)
                    {
                        seasonStat = new PlayerSeasonStatistic
                        {
                            PlayerId = player.PlayerId,
                            Season = stat.league.season,
                            LeagueId = 1,
                            TeamId = localTeamId
                        };

                        _context.PlayerSeasonStatistics.Add(seasonStat);
                        existingStats.Add(seasonStat);
                    }

                    seasonStat.Appearances = stat.games?.appearances;
                    seasonStat.Lineups = stat.games?.lineups;
                    seasonStat.Minutes = stat.games?.minutes;

                    if (decimal.TryParse(stat.games?.rating, out var rating))
                        seasonStat.Rating = rating;
                    else
                        seasonStat.Rating = null;

                    seasonStat.Goals = stat.goals?.total;
                    seasonStat.Assists = stat.goals?.assists;
                    seasonStat.YellowCards = stat.cards?.yellow;
                    seasonStat.RedCards = stat.cards?.red;
                }
            }

            var result = await _context.Players
                .Where(p => p.TeamId == team.TeamId)
                .Include(p => p.PlayerSeasonStatistics)
                .Select(p => new PlayerWithStatsDto
                {
                    PlayerId = p.PlayerId,
                    FullName = p.FullName,
                    Nationality = p.Nationality,
                    HeightCm = p.HeightCm,
                    WeightKg = p.WeightKg,
                    PhotoUrl = p.PhotoUrl,

                    Statistics = p.PlayerSeasonStatistics
                        .Where(s => s.Season == season)
                        .Select(s => new PlayerSeasonStatDto
                        {
                            Season = s.Season,
                            LeagueId = s.LeagueId,
                            TeamId = s.TeamId,
                            Appearances = s.Appearances,
                            Lineups = s.Lineups,
                            Minutes = s.Minutes,
                            Goals = s.Goals,
                            Assists = s.Assists,
                            YellowCards = s.YellowCards,
                            RedCards = s.RedCards,
                            Rating = s.Rating
                        })
                        .ToList()
                })
                .ToListAsync();

            return result;
        }

        public async Task<List<PlayerWithStatsDto>> SyncPlayersByLeagueAsync(int season)
        {
            var teams = await _context.Teams
                .Where(t => t.ApiTeamId != null)
                .ToListAsync();

            var allPlayers = new List<PlayerWithStatsDto>();

            foreach (var team in teams)
            {
                var players = await SyncPlayersByTeamAsync(team.ApiTeamId!.Value, season);

                if (players != null && players.Any())
                    allPlayers.AddRange(players);

                await Task.Delay(800);
            }

            return allPlayers;
        }

        public async Task SyncPlayerTransfersAsync(Player player)
        {
            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballTransferResponse>(
                    $"transfers?player={player.ApiPlayerId}");

            if (response?.response == null || !response.response.Any())
                return;

            var existingContracts = await _context.Contracts
                .Where(c => c.PlayerId == player.PlayerId)
                .ToListAsync();

            foreach (var wrapper in response.response)
            {
                if (wrapper.transfers == null || !wrapper.transfers.Any())
                    continue;

                foreach (var transfer in wrapper.transfers)
                {
                    if (transfer?.teams == null)
                        continue;

                    var transferDate = transfer.date;

                    var fromTeamId = transfer.teams.@out?.id;
                    var toTeamId = transfer.teams.@in?.id;

                    var alreadyExists = existingContracts.Any(c =>
                        c.TransferDate == transferDate &&
                        c.FromTeamApiId == fromTeamId &&
                        c.ToTeamApiId == toTeamId);

                    if (alreadyExists)
                        continue;

                    var contract = new Contract
                    {
                        PlayerId = player.PlayerId,
                        TransferDate = transferDate,

                        FromTeamApiId = fromTeamId,
                        ToTeamApiId = toTeamId,

                        FromTeamName = transfer.teams.@out?.name,
                        ToTeamName = transfer.teams.@in?.name,

                        TransferType = transfer.type
                    };

                    _context.Contracts.Add(contract);
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
