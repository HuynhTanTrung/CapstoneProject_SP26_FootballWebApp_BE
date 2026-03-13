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
using VNFootballLeagues.Services.Models.Api.GetMatches;
using VNFootballLeagues.Services.Models.Api.GetPlayer;
using VNFootballLeagues.Services.Models.Api.GetStanding;
using VNFootballLeagues.Services.Models.Api.GetTransfer;

namespace VNFootballLeagues.Services.Services
{
    public class FootballApiService : IFootballApiService
    {
        private readonly HttpClient _httpClient;
        private readonly VNFootballLeaguesDBContext _context;
        //private const string ApiKey = "6eb9790bc76fca11467f05ff4386793a";
        private const string ApiKey = "a62490994cdd84b4c3ed053a5425321b";
        private const string BaseUrl = "https://v3.football.api-sports.io/";
        private readonly int[] VietnamLeagueApiIds = { 340, 341, 637 };

        public FootballApiService(HttpClient httpClient, VNFootballLeaguesDBContext context)
        {
            _context = context;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("x-apisports-key", ApiKey);
        }
        private decimal? ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            value = value.Replace(" cm", "").Replace(" kg", "");

            if (decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result))
                return result;

            return null;
        }

        public async Task<List<Season>> SyncSeasonsAsync()
        {
            var seasonsAdded = new List<Season>();

            foreach (var apiLeagueId in VietnamLeagueApiIds)
            {
                var league = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

                if (league == null)
                    continue;

                var response = await _httpClient
                    .GetFromJsonAsync<ApiFootballLeagueResponse>($"leagues?id={apiLeagueId}");

                if (response?.response == null || !response.response.Any())
                    continue;

                var seasons = response.response.First().seasons;

                foreach (var apiSeason in seasons)
                {
                    var existingSeason = await _context.Seasons
                        .FirstOrDefaultAsync(s =>
                            s.LeagueId == league.LeagueId &&
                            s.Year == apiSeason.year);

                    if (existingSeason != null)
                        continue;

                    var newSeason = new Season
                    {
                        LeagueId = league.LeagueId,
                        Year = apiSeason.year,

                        StartDate = !string.IsNullOrEmpty(apiSeason.start)
                            ? DateOnly.Parse(apiSeason.start)
                            : null,

                        EndDate = !string.IsNullOrEmpty(apiSeason.end)
                            ? DateOnly.Parse(apiSeason.end)
                            : null,

                        IsCurrent = apiSeason.current,
                        IsCurrentSeason = apiSeason.current,

                        ApiCoverage = apiSeason.coverage != null
                            ? System.Text.Json.JsonSerializer.Serialize(apiSeason.coverage)
                            : null
                    };

                    _context.Seasons.Add(newSeason);
                    seasonsAdded.Add(newSeason);
                }
            }

            await _context.SaveChangesAsync();

            return seasonsAdded;
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

        public async Task<List<Player>> SyncPlayersByLeagueAsync(int apiLeagueId, int seasonYear)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced first.");

            var dbLeagueId = league.LeagueId;

            var teams = await _context.Teams
                .Where(t => t.LeagueId == dbLeagueId)
                .ToDictionaryAsync(t => t.ApiTeamId);

            if (!teams.Any())
                throw new Exception("Teams must be synced first.");

            var playersDb = await _context.Players
                .ToDictionaryAsync(p => p.ApiPlayerId);

            var page = 1;
            var allPlayers = new List<ApiPlayerWrapper>();

            while (true)
            {
                var response = await _httpClient
                    .GetFromJsonAsync<ApiFootballPlayerResponse>(
                        $"players?league={apiLeagueId}&season={seasonYear}&page={page}");

                if (response?.response == null || !response.response.Any())
                    break;

                allPlayers.AddRange(response.response);

                if (page >= response.paging.total)
                    break;

                page++;
            }

            var syncedPlayers = new List<Player>();

            foreach (var wrapper in allPlayers)
            {
                var apiPlayer = wrapper.player;
                var stat = wrapper.statistics?.FirstOrDefault();

                if (stat == null)
                    continue;

                if (!teams.TryGetValue(stat.team.id, out var team))
                    continue;

                if (!playersDb.TryGetValue(apiPlayer.id, out var player))
                {
                    player = new Player
                    {
                        ApiPlayerId = apiPlayer.id,
                        FirstName = apiPlayer.firstname,
                        LastName = apiPlayer.lastname,
                        FullName = apiPlayer.name,
                        Age = apiPlayer.age,
                        Nationality = apiPlayer.nationality,
                        BirthPlace = apiPlayer.birth?.place,
                        BirthCountry = apiPlayer.birth?.country,
                        DateOfBirth = DateTime.TryParse(apiPlayer.birth?.date, out var dob)
                            ? DateOnly.FromDateTime(dob)
                            : null,
                        HeightCm = ParseDecimal(apiPlayer.height),
                        WeightKg = ParseDecimal(apiPlayer.weight),
                        PhotoUrl = apiPlayer.photo,
                        IsInjured = apiPlayer.injured ?? false,
                        Position = stat.games?.position,
                        Number = stat.games?.number,
                        TeamId = team.TeamId
                    };

                    _context.Players.Add(player);
                    playersDb[apiPlayer.id] = player;
                    syncedPlayers.Add(player);
                }
                else
                {
                    player.Age = apiPlayer.age;
                    player.IsInjured = apiPlayer.injured ?? false;
                    player.TeamId = team.TeamId;
                }
            }

            await _context.SaveChangesAsync();

            return syncedPlayers;
        }

        public async Task<List<PlayerSeasonStatistic>> SyncPlayerSeasonStatisticsAsync(int apiLeagueId, int seasonYear)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced first.");

            var dbLeagueId = league.LeagueId;

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s =>
                    s.Year == seasonYear &&
                    s.LeagueId == dbLeagueId);

            if (season == null)
                throw new Exception("Season must be synced first.");

            var players = await _context.Players
                .ToDictionaryAsync(p => p.ApiPlayerId);

            var teams = await _context.Teams
                .ToDictionaryAsync(t => t.ApiTeamId);

            var statsDb = await _context.PlayerSeasonStatistics
                .Where(s => s.SeasonId == season.SeasonId)
                .ToListAsync();

            var page = 1;
            var syncedStats = new List<PlayerSeasonStatistic>();

            while (true)
            {
                var response = await _httpClient
                    .GetFromJsonAsync<ApiFootballPlayerResponse>(
                        $"players?league={apiLeagueId}&season={seasonYear}&page={page}");

                if (response?.response == null || !response.response.Any())
                    break;

                foreach (var wrapper in response.response)
                {
                    var apiPlayer = wrapper.player;
                    var stat = wrapper.statistics?.FirstOrDefault();

                    if (stat == null)
                        continue;

                    if (!players.TryGetValue(apiPlayer.id, out var player))
                        continue;

                    if (!teams.TryGetValue(stat.team.id, out var team))
                        continue;

                    var existingStat = statsDb.FirstOrDefault(s =>
                        s.PlayerId == player.PlayerId &&
                        s.SeasonId == season.SeasonId);

                    if (existingStat == null)
                    {
                        existingStat = new PlayerSeasonStatistic
                        {
                            PlayerId = player.PlayerId,
                            SeasonId = season.SeasonId,
                            LeagueId = dbLeagueId,
                            TeamId = team.TeamId
                        };

                        _context.PlayerSeasonStatistics.Add(existingStat);
                        statsDb.Add(existingStat);
                    }

                    existingStat.Appearances = stat.games?.appearances ?? 0;
                    existingStat.Lineups = stat.games?.lineups ?? 0;
                    existingStat.Minutes = stat.games?.minutes ?? 0;

                    existingStat.Goals = stat.goals?.total ?? 0;
                    existingStat.Assists = stat.goals?.assists ?? 0;

                    existingStat.YellowCards = stat.cards?.yellow ?? 0;
                    existingStat.RedCards = stat.cards?.red ?? 0;

                    existingStat.Rating = ParseDecimal(stat.games?.rating);

                    syncedStats.Add(existingStat);
                }

                if (page >= response.paging.total)
                    break;

                page++;
            }

            await _context.SaveChangesAsync();

            return syncedStats;
        }

        public async Task<List<Match>> SyncMatchesByLeagueAsync(int apiLeagueId, int season)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced first.");

            var dbLeagueId = league.LeagueId;

            var dbSeason = await _context.Seasons
                .FirstOrDefaultAsync(s =>
                    s.Year == season &&
                    s.LeagueId == dbLeagueId);

            if (dbSeason == null)
                throw new Exception("Season must be synced first.");

            var teams = await _context.Teams
                .Where(t => t.LeagueId == dbLeagueId)
                .ToDictionaryAsync(t => t.ApiTeamId);

            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballMatchResponse>(
                    $"fixtures?league={apiLeagueId}&season={season}");

            if (response?.response == null)
                return new List<Match>();

            foreach (var item in response.response)
            {
                if (item.fixture == null || item.teams?.home == null || item.teams?.away == null)
                    continue;

                var matchApi = item.fixture;

                if (!teams.TryGetValue(item.teams.home.id, out var homeTeam))
                    continue;

                if (!teams.TryGetValue(item.teams.away.id, out var awayTeam))
                    continue;

                var existing = await _context.Matches
                    .FirstOrDefaultAsync(m => m.ApiFixtureId == matchApi.id);

                var matchDate = DateTime.Parse(matchApi.date);

                if (existing == null)
                {
                    var match = new Match
                    {
                        ApiFixtureId = matchApi.id,

                        LeagueId = dbLeagueId,
                        SeasonId = dbSeason.SeasonId,

                        MatchDate = matchDate,
                        KickOffTime = TimeOnly.FromDateTime(matchDate),

                        Status = matchApi.status?.@long,
                        Attendance = matchApi.attendance,

                        HomeTeamId = homeTeam.TeamId,
                        AwayTeamId = awayTeam.TeamId,

                        HomeGoals = item.goals?.home,
                        AwayGoals = item.goals?.away,

                        Venue = matchApi.venue?.name,
                        ApiVenueId = matchApi.venue?.id,

                        RefereeName = matchApi.referee,

                        ApiTimestamp = (int)matchApi.timestamp,
                        Timezone = matchApi.timezone,

                        PeriodFirstHalf = matchApi.periods?.first,
                        PeriodSecondHalf = matchApi.periods?.second,

                        Round = item.league?.round,
                       
                    };

                    _context.Matches.Add(match);
                }
                else
                {
                    existing.Status = matchApi.status?.@long;
                    existing.HomeGoals = item.goals?.home;
                    existing.AwayGoals = item.goals?.away;
                }
            }

            await _context.SaveChangesAsync();

            return await _context.Matches
                .Where(m => m.LeagueId == dbLeagueId && m.SeasonId == dbSeason.SeasonId)
                .ToListAsync();
        }

        public async Task<List<Standing>> SyncStandingsAsync(int apiLeagueId, int seasonYear)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiLeagueId);

            if (league == null)
                throw new Exception("League must be synced first.");

            var dbLeagueId = league.LeagueId;

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s =>
                    s.Year == seasonYear &&
                    s.LeagueId == dbLeagueId);

            if (season == null)
                throw new Exception("Season must be synced first.");

            var teams = await _context.Teams
                .Where(t => t.LeagueId == dbLeagueId)
                .ToDictionaryAsync(t => t.ApiTeamId);

            var response = await _httpClient
                .GetFromJsonAsync<ApiFootballStandingResponse>(
                    $"standings?league={apiLeagueId}&season={seasonYear}");

            if (response?.response == null || !response.response.Any())
                return new List<Standing>();

            var standingsApi = response.response
                .First().league.standings
                .First();

            foreach (var item in standingsApi)
            {
                if (!teams.TryGetValue(item.team.id, out var team))
                    continue;

                var existing = await _context.Standings
                    .FirstOrDefaultAsync(s =>
                        s.TeamId == team.TeamId &&
                        s.SeasonId == season.SeasonId);

                if (existing == null)
                {
                    existing = new Standing
                    {
                        LeagueId = dbLeagueId,
                        SeasonId = season.SeasonId,
                        TeamId = team.TeamId
                    };

                    _context.Standings.Add(existing);
                }

                existing.Rank = item.rank;
                existing.Points = item.points;

                existing.Played = item.all?.played;
                existing.Win = item.all?.win;
                existing.Draw = item.all?.draw;
                existing.Loss = item.all?.lose;

                existing.GoalsFor = item.all?.goals?.forValue;
                existing.GoalsAgainst = item.all?.goals?.against;
                existing.GoalDifference = item.goalsDiff;

                existing.Form = item.form;
                existing.Status = item.status;
                existing.Description = item.description;

                existing.HomeRecord =
                    $"{item.home?.win}-{item.home?.draw}-{item.home?.lose}";

                existing.AwayRecord =
                    $"{item.away?.win}-{item.away?.draw}-{item.away?.lose}";

                existing.ApiLastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return await _context.Standings
                .Include(s => s.Team)
                .Where(s =>
                    s.LeagueId == dbLeagueId &&
                    s.SeasonId == season.SeasonId)
                .OrderBy(s => s.Rank)
                .ToListAsync();
        }

        // GET methods - retrieve synced data from database
        public async Task<List<League>> GetLeaguesAsync()
        {
            return await _context.Leagues
                .OrderBy(l => l.LeagueName)
                .ToListAsync();
        }

        public async Task<List<Season>> GetSeasonsAsync(int? leagueId = null)
        {
            var query = _context.Seasons
                .Include(s => s.League)
                .AsQueryable();

            if (leagueId.HasValue)
                query = query.Where(s => s.LeagueId == leagueId.Value);

            return await query
                .OrderByDescending(s => s.Year)
                .ToListAsync();
        }

        public async Task<List<Team>> GetTeamsAsync(int? leagueId = null)
        {
            var query = _context.Teams
                .Include(t => t.League)
                .Include(t => t.Stadium)
                .AsQueryable();

            if (leagueId.HasValue)
                query = query.Where(t => t.LeagueId == leagueId.Value);

            return await query
                .OrderBy(t => t.TeamName)
                .ToListAsync();
        }

        public async Task<List<Player>> GetPlayersAsync(int? teamId = null)
        {
            var query = _context.Players
                .Include(p => p.Team)
                .AsQueryable();

            if (teamId.HasValue)
                query = query.Where(p => p.TeamId == teamId.Value);

            return await query
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToListAsync();
        }

        public async Task<List<PlayerSeasonStatistic>> GetPlayerStatsAsync(int? playerId = null, int? seasonId = null)
        {
            var query = _context.PlayerSeasonStatistics
                .Include(s => s.Player)
                .Include(s => s.Season)
                .Include(s => s.Team)
                .Include(s => s.League)
                .AsQueryable();

            if (playerId.HasValue)
                query = query.Where(s => s.PlayerId == playerId.Value);

            if (seasonId.HasValue)
                query = query.Where(s => s.SeasonId == seasonId.Value);

            return await query
                .OrderByDescending(s => s.Goals)
                .ToListAsync();
        }

        public async Task<List<Match>> GetMatchesAsync(int? leagueId = null, int? seasonId = null)
        {
            var query = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.League)
                .Include(m => m.Season)
                .AsQueryable();

            if (leagueId.HasValue)
                query = query.Where(m => m.LeagueId == leagueId.Value);

            if (seasonId.HasValue)
                query = query.Where(m => m.SeasonId == seasonId.Value);

            return await query
                .OrderByDescending(m => m.MatchDate)
                .ToListAsync();
        }

        public async Task<List<Standing>> GetStandingsAsync(int? leagueId = null, int? seasonId = null)
        {
            var query = _context.Standings
                .Include(s => s.Team)
                .Include(s => s.League)
                .Include(s => s.Season)
                .AsQueryable();

            if (leagueId.HasValue)
                query = query.Where(s => s.LeagueId == leagueId.Value);

            if (seasonId.HasValue)
                query = query.Where(s => s.SeasonId == seasonId.Value);

            return await query
                .OrderBy(s => s.Rank)
                .ToListAsync();
        }
    }
}
