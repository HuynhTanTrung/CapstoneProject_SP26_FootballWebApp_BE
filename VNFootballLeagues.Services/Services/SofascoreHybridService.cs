using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Net;
using System.Text.Json;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services
{
    public class SofascoreHybridService : ISofascoreHybridService, IAsyncDisposable
    {
        private readonly VNFootballLeaguesDBContext _context;
        private readonly ILogger<SofascoreHybridService> _logger;
        private static IBrowser _browser;
        private static bool _initialized = false;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static int _activePages = 0;
        private static readonly object _pageCountLock = new();

        public SofascoreHybridService(
            VNFootballLeaguesDBContext context,
            ILogger<SofascoreHybridService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Browser Management

        private async Task InitBrowser()
        {
            if (_initialized) return;

            await _lock.WaitAsync();
            try
            {
                if (_initialized) return;

                // Use a specific browser path for Azure
                var options = new LaunchOptions
                {
                    Headless = true,
                    Args = new[]
                    {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-accelerated-2d-canvas",
                "--disable-gpu",
                "--window-size=1920,1080",
                "--disable-web-security",
                "--disable-features=VizDisplayCompositor",
                "--disable-extensions",
                "--disable-component-extensions-with-background-pages",
                "--disable-default-apps",
                "--mute-audio",
                "--no-first-run",
                "--disable-background-networking",
                "--disable-sync",
                "--disable-translate"
            },
                    Timeout = 60000, // Increased timeout for Azure
                    ExecutablePath = GetChromeExecutablePath() // Set specific path
                };

                _browser = await Puppeteer.LaunchAsync(options);
                _initialized = true;
                _logger.LogInformation("Browser initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize browser");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        private string GetChromeExecutablePath()
        {
            // Try different possible paths for Chrome in Azure
            var possiblePaths = new[]
            {
        Path.Combine(Directory.GetCurrentDirectory(), "Chrome", "chrome.exe"),
        Path.Combine(Directory.GetCurrentDirectory(), "chrome-win", "chrome.exe"),
        Path.Combine(Directory.GetCurrentDirectory(), "bin", "chrome", "chrome.exe"),
        // On Linux Azure App Service
        "/usr/bin/google-chrome",
        "/usr/bin/chromium-browser",
        "/usr/bin/chromium"
    };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation($"Found Chrome at: {path}");
                    return path;
                }
            }

            return null; // Let Puppeteer find it automatically
        }

        private async Task<string> FetchJson(string url, int retryCount = 2)
        {
            await InitBrowser();

            IPage page = null;
            try
            {
                lock (_pageCountLock)
                {
                    if (_activePages >= 5)
                    {
                        throw new InvalidOperationException("Too many concurrent requests");
                    }
                    _activePages++;
                }

                page = await _browser.NewPageAsync();

                // Set default timeout for all operations on this page
                page.DefaultTimeout = 30000;

                // Or set navigation timeout specifically
                page.DefaultNavigationTimeout = 30000;

                await page.SetUserAgentAsync(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                );

                await page.SetCacheEnabledAsync(false);

                await page.EvaluateExpressionOnNewDocumentAsync(@"
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });
        ");

                var response = await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                    Timeout = 30000
                });

                if (response.Status != HttpStatusCode.OK &&
                    response.Status != HttpStatusCode.NotModified)
                {
                    throw new HttpRequestException($"HTTP Error: {response.Status}");
                }

                await Task.Delay(500);

                var content = await page.EvaluateExpressionAsync<string>("document.body.innerText");

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("Empty response");
                }

                try
                {
                    JsonDocument.Parse(content);
                    return content;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Invalid JSON received from {Url}", url);
                    throw;
                }
            }
            catch (Exception ex) when (retryCount > 0)
            {
                _logger.LogWarning(ex, "Failed to fetch {Url}, retrying...", url);
                await Task.Delay(1000);
                return await FetchJson(url, retryCount - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch {Url}", url);
                throw;
            }
            finally
            {
                if (page != null)
                {
                    await page.CloseAsync();
                }
                lock (_pageCountLock)
                {
                    _activePages--;
                }
            }
        }

        #endregion

        #region Match Methods
        public async Task<object> SyncMatchesByRoundAsync(int tournamentId, int seasonId)
        {
            try
            {
                var roundsUrl =
                    $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/rounds";

                var roundsJson = await FetchJson(roundsUrl);
                using var roundsDoc = JsonDocument.Parse(roundsJson);

                if (!roundsDoc.RootElement.TryGetProperty("rounds", out var rounds))
                {
                    return new
                    {
                        status = false,
                        message = "No rounds found",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;
                var processedMatches = new HashSet<int>();

                foreach (var r in rounds.EnumerateArray())
                {
                    int round = r.GetProperty("round").GetInt32();

                    var url =
                        $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/events/round/{round}";

                    var json = await FetchJson(url);
                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("events", out var events))
                        continue;

                    foreach (var ev in events.EnumerateArray())
                    {
                        var apiId = ev.GetProperty("id").GetInt32();

                        if (processedMatches.Contains(apiId))
                            continue;

                        processedMatches.Add(apiId);

                        var existingMatch = await _context.Matches
                            .FirstOrDefaultAsync(x => x.ApiFixtureId == apiId);

                        var matchDate = DateTimeOffset
                            .FromUnixTimeSeconds(ev.GetProperty("startTimestamp").GetInt64())
                            .DateTime;

                        int? homeTeamId = null;
                        int? awayTeamId = null;

                        if (ev.TryGetProperty("homeTeam", out var homeTeam))
                        {
                            homeTeamId = homeTeam.GetProperty("id").GetInt32();
                        }

                        if (ev.TryGetProperty("awayTeam", out var awayTeam))
                        {
                            awayTeamId = awayTeam.GetProperty("id").GetInt32();
                        }

                        if (existingMatch == null)
                        {
                            _context.Matches.Add(new Match
                            {
                                ApiFixtureId = apiId,
                                LeagueId = tournamentId,
                                SeasonId = seasonId,
                                MatchDate = matchDate,
                                KickOffTime = TimeOnly.FromDateTime(matchDate),
                                ApiTimestamp = (int)ev.GetProperty("startTimestamp").GetInt64(),
                                HomeGoals = SafeScore(ev, "homeScore"),
                                AwayGoals = SafeScore(ev, "awayScore"),
                                HomeTeamId = homeTeamId,
                                AwayTeamId = awayTeamId,
                                Status = ev.GetProperty("status").GetProperty("type").GetString(),
                                Round = ev.GetProperty("roundInfo").GetProperty("round").GetInt32().ToString(),
                                Venue = ev.TryGetProperty("venue", out var venue) &&
                                        venue.TryGetProperty("stadium", out var stadium)
                                        ? stadium.GetProperty("name").GetString()
                                        : null,
                                ApiVenueId = ev.TryGetProperty("venue", out var v2) &&
                                             v2.TryGetProperty("id", out var vid)
                                            ? vid.GetInt32()
                                            : null,
                                RefereeName = ev.TryGetProperty("referee", out var refEl)
                                            ? refEl.GetProperty("name").GetString()
                                            : null,
                                Attendance = ev.TryGetProperty("attendance", out var att)
                                            ? att.GetInt32()
                                            : null
                            });
                            added++;
                        }
                        else
                        {
                            existingMatch.MatchDate = matchDate;
                            existingMatch.KickOffTime = TimeOnly.FromDateTime(matchDate);
                            existingMatch.ApiTimestamp = (int)ev.GetProperty("startTimestamp").GetInt64();
                            existingMatch.HomeGoals = SafeScore(ev, "homeScore");
                            existingMatch.AwayGoals = SafeScore(ev, "awayScore");
                            existingMatch.Status = ev.GetProperty("status").GetProperty("type").GetString();
                            existingMatch.Venue = ev.TryGetProperty("venue", out var venue) &&
                                                  venue.TryGetProperty("stadium", out var stadium)
                                                  ? stadium.GetProperty("name").GetString()
                                                  : existingMatch.Venue;
                            existingMatch.RefereeName = ev.TryGetProperty("referee", out var refEl)
                                                      ? refEl.GetProperty("name").GetString()
                                                      : existingMatch.RefereeName;
                            existingMatch.Attendance = ev.TryGetProperty("attendance", out var att)
                                                      ? att.GetInt32()
                                                      : existingMatch.Attendance;

                            _context.Matches.Update(existingMatch);
                            updated++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Inserted {added} matches, Updated {updated} matches",
                    data = new { added, updated }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncMatchesByRoundAsync");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = (object)null
                };
            }
        }

        #endregion

        #region Team Statistics Methods

        public async Task<object> SyncMatchStatisticsAsync(int apiFixtureId)
        {
            try
            {
                var match = await _context.Matches
                    .FirstOrDefaultAsync(m => m.ApiFixtureId == apiFixtureId);

                if (match == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Match with ApiFixtureId {apiFixtureId} not found",
                        data = (object)null
                    };
                }

                var statisticsUrl = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/statistics";
                var json = await FetchJson(statisticsUrl);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("statistics", out var statistics))
                {
                    return new
                    {
                        status = false,
                        message = $"No statistics found for match {apiFixtureId}",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;

                if (statistics.ValueKind == JsonValueKind.Array)
                {
                    foreach (var period in statistics.EnumerateArray())
                    {
                        var result = await ProcessStatisticsPeriod(period, match.MatchId);
                        added += result.added;
                        updated += result.updated;
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Saved statistics for match {apiFixtureId} (Added: {added}, Updated: {updated})",
                    data = new { added, updated, matchId = match.MatchId, apiFixtureId }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncMatchStatisticsAsync");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        private async Task<(int added, int updated)> ProcessStatisticsPeriod(JsonElement period, int matchId)
        {
            int added = 0;
            int updated = 0;

            if (period.TryGetProperty("groups", out var groups))
            {
                foreach (var group in groups.EnumerateArray())
                {
                    if (group.TryGetProperty("statisticsItems", out var statisticsItems))
                    {
                        var homeStats = ExtractTeamStatistics(statisticsItems, "home");
                        if (homeStats != null)
                        {
                            homeStats.MatchId = matchId;
                            var result = await SaveOrUpdateStatisticsAsync(homeStats, matchId, "home");
                            if (result) added++;
                        }

                        var awayStats = ExtractTeamStatistics(statisticsItems, "away");
                        if (awayStats != null)
                        {
                            awayStats.MatchId = matchId;
                            var result = await SaveOrUpdateStatisticsAsync(awayStats, matchId, "away");
                            if (result) added++;
                        }
                    }
                }
            }

            return (added, updated);
        }

        private MatchStatistic ExtractTeamStatistics(JsonElement statisticsItems, string teamType)
        {
            var teamStats = new MatchStatistic();

            foreach (var stat in statisticsItems.EnumerateArray())
            {
                string statName = null;
                if (stat.TryGetProperty("name", out var name))
                {
                    statName = name.GetString();
                }

                if (string.IsNullOrEmpty(statName)) continue;

                string statValue = null;
                if (teamType == "home" && stat.TryGetProperty("home", out var homeValue))
                {
                    statValue = homeValue.ValueKind == JsonValueKind.String ? homeValue.GetString() :
                               homeValue.ValueKind == JsonValueKind.Number ? homeValue.GetRawText() : null;
                }
                else if (teamType == "away" && stat.TryGetProperty("away", out var awayValue))
                {
                    statValue = awayValue.ValueKind == JsonValueKind.String ? awayValue.GetString() :
                               awayValue.ValueKind == JsonValueKind.Number ? awayValue.GetRawText() : null;
                }

                if (string.IsNullOrEmpty(statValue)) continue;

                switch (statName.ToLower())
                {
                    case "ball possession":
                        teamStats.Possession = ParseIntPercentage(statValue);
                        break;
                    case "expected goals":
                        teamStats.ExpectedGoals = ParseDecimal(statValue);
                        break;
                    case "total shots":
                        teamStats.Shots = ParseInt(statValue);
                        break;
                    case "shots on target":
                        teamStats.ShotsOnTarget = ParseInt(statValue);
                        break;
                    case "corner kicks":
                        teamStats.Corners = ParseInt(statValue);
                        break;
                    case "fouls":
                        teamStats.Fouls = ParseInt(statValue);
                        break;
                    case "yellow cards":
                        teamStats.YellowCards = ParseInt(statValue);
                        break;
                    case "red cards":
                        teamStats.RedCards = ParseInt(statValue);
                        break;
                    case "offsides":
                        teamStats.Offsides = ParseInt(statValue);
                        break;
                    case "blocked shots":
                        teamStats.ShotsBlocked = ParseInt(statValue);
                        break;
                    case "shots inside box":
                        teamStats.ShotsInsideBox = ParseInt(statValue);
                        break;
                    case "shots outside box":
                        teamStats.ShotsOutsideBox = ParseInt(statValue);
                        break;
                    case "total saves":
                    case "goalkeeper saves":
                        teamStats.Saves = ParseInt(statValue);
                        break;
                    case "interceptions":
                        teamStats.Interceptions = ParseInt(statValue);
                        break;
                    case "clearances":
                        teamStats.Clearances = ParseInt(statValue);
                        break;
                    case "total tackles":
                        teamStats.TacklesWon = ParseInt(statValue);
                        break;
                }
            }

            if (teamStats.Possession == null && teamStats.Shots == null &&
                teamStats.ShotsOnTarget == null && teamStats.Corners == null)
            {
                return null;
            }

            return teamStats;
        }

        private async Task<bool> SaveOrUpdateStatisticsAsync(MatchStatistic stats, int matchId, string teamType)
        {
            var match = await _context.Matches.FirstOrDefaultAsync(m => m.MatchId == matchId);
            if (match == null) return false;

            int? teamId = teamType == "home" ? match.HomeTeamId : match.AwayTeamId;
            if (teamId == null) return false;

            stats.TeamId = teamId;

            var existingStats = await _context.MatchStatistics
                .FirstOrDefaultAsync(s => s.MatchId == matchId && s.TeamId == teamId);

            if (existingStats != null)
            {
                existingStats.Possession = stats.Possession ?? existingStats.Possession;
                existingStats.Shots = stats.Shots ?? existingStats.Shots;
                existingStats.ShotsOnTarget = stats.ShotsOnTarget ?? existingStats.ShotsOnTarget;
                existingStats.Corners = stats.Corners ?? existingStats.Corners;
                existingStats.Fouls = stats.Fouls ?? existingStats.Fouls;
                existingStats.YellowCards = stats.YellowCards ?? existingStats.YellowCards;
                existingStats.RedCards = stats.RedCards ?? existingStats.RedCards;
                existingStats.Offsides = stats.Offsides ?? existingStats.Offsides;
                existingStats.ShotsBlocked = stats.ShotsBlocked ?? existingStats.ShotsBlocked;
                existingStats.ShotsInsideBox = stats.ShotsInsideBox ?? existingStats.ShotsInsideBox;
                existingStats.ShotsOutsideBox = stats.ShotsOutsideBox ?? existingStats.ShotsOutsideBox;
                existingStats.PassesAccuracy = stats.PassesAccuracy ?? existingStats.PassesAccuracy;
                existingStats.PassesKey = stats.PassesKey ?? existingStats.PassesKey;
                existingStats.DribblesAttempted = stats.DribblesAttempted ?? existingStats.DribblesAttempted;
                existingStats.DribblesSuccess = stats.DribblesSuccess ?? existingStats.DribblesSuccess;
                existingStats.DuelsWon = stats.DuelsWon ?? existingStats.DuelsWon;
                existingStats.DuelsTotal = stats.DuelsTotal ?? existingStats.DuelsTotal;
                existingStats.TacklesWon = stats.TacklesWon ?? existingStats.TacklesWon;
                existingStats.Saves = stats.Saves ?? existingStats.Saves;
                existingStats.Interceptions = stats.Interceptions ?? existingStats.Interceptions;
                existingStats.Clearances = stats.Clearances ?? existingStats.Clearances;
                existingStats.ExpectedGoals = stats.ExpectedGoals ?? existingStats.ExpectedGoals;

                _context.MatchStatistics.Update(existingStats);
            }
            else
            {
                await _context.MatchStatistics.AddAsync(stats);
            }

            return true;
        }

        #endregion

        #region Helper Methods

        private int SafeScore(JsonElement ev, string key)
        {
            try
            {
                if (ev.TryGetProperty(key, out var score) &&
                    score.TryGetProperty("current", out var current) &&
                    current.ValueKind != JsonValueKind.Null)
                {
                    return current.GetInt32();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing score for key {Key}", key);
            }
            return 0;
        }

        private int? ParseInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (int.TryParse(value, out var result)) return result;
            return null;
        }

        private int? ParseIntPercentage(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var cleanValue = value.Replace("%", "");
            if (int.TryParse(cleanValue, out var result)) return result;
            return null;
        }

        private decimal? ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result)) return result;
            return null;
        }

        #endregion

        #region Dispose

        public async ValueTask DisposeAsync()
        {
            if (_browser != null && _initialized)
            {
                await _browser.CloseAsync();
                await _browser.DisposeAsync();
                _initialized = false;
                _logger.LogInformation("Browser disposed");
            }
        }

        #endregion

        public async Task<object> SyncTeamsFromStandingsAsync(int tournamentId, int seasonId)
        {
            try
            {
                // Fetch standings data
                string standingsJson = await GetTournamentStandingsAsync(tournamentId, seasonId);

                using var doc = JsonDocument.Parse(standingsJson);

                var teamsList = new List<(int id, string name, string shortName, string logoUrl)>();

                // Parse standings to extract teams
                if (doc.RootElement.TryGetProperty("standings", out var standings))
                {
                    foreach (var standingGroup in standings.EnumerateArray())
                    {
                        if (standingGroup.TryGetProperty("rows", out var rows))
                        {
                            foreach (var row in rows.EnumerateArray())
                            {
                                if (row.TryGetProperty("team", out var team))
                                {
                                    var id = team.GetProperty("id").GetInt32();
                                    var name = team.GetProperty("name").GetString();
                                    var shortName = team.TryGetProperty("shortName", out var shortProp) ? shortProp.GetString() : null;

                                    // Extract logo URL - Sofascore often uses "image" field
                                    string logoUrl = null;
                                    if (team.TryGetProperty("image", out var imageProp))
                                    {
                                        logoUrl = imageProp.GetString();
                                    }
                                    // Some versions might use "logo" or "icon"
                                    else if (team.TryGetProperty("logo", out var logoProp))
                                    {
                                        logoUrl = logoProp.GetString();
                                    }
                                    else if (team.TryGetProperty("icon", out var iconProp))
                                    {
                                        logoUrl = iconProp.GetString();
                                    }

                                    teamsList.Add((id, name, shortName, logoUrl));

                                    // Log to see what we're getting
                                    _logger.LogDebug($"Team {id}: {name}, Logo: {logoUrl ?? "null"}");
                                }
                            }
                        }
                    }
                }

                if (teamsList.Count == 0)
                {
                    return new
                    {
                        status = false,
                        message = "No teams found in standings data",
                        data = (object)null
                    };
                }

                _logger.LogInformation($"Found {teamsList.Count} teams in standings for tournament {tournamentId}, season {seasonId}");

                int added = 0;
                int updated = 0;

                foreach (var teamData in teamsList)
                {
                    var existingTeam = await _context.Teams
                        .FirstOrDefaultAsync(t => t.ApiTeamId == teamData.id && t.LeagueId == tournamentId);

                    if (existingTeam == null)
                    {
                        // Create new team with ClubId = 1
                        var newTeam = new Team
                        {
                            TeamName = teamData.name,
                            ClubId = 1,
                            ApiTeamId = teamData.id,
                            LogoUrl = teamData.logoUrl,  // This should now have the logo URL
                            ShortName = teamData.shortName,
                            Founded = null,
                            National = false,
                            LeagueId = tournamentId,
                            StadiumId = null
                        };

                        _context.Teams.Add(newTeam);
                        added++;
                    }
                    else
                    {
                        // Update existing team
                        existingTeam.TeamName = teamData.name;
                        existingTeam.LogoUrl = teamData.logoUrl ?? existingTeam.LogoUrl;  // Update logo if available
                        existingTeam.ShortName = teamData.shortName ?? existingTeam.ShortName;
                        existingTeam.ClubId = 1;

                        _context.Teams.Update(existingTeam);
                        updated++;
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Inserted {added} teams, Updated {updated} teams",
                    data = new
                    {
                        added,
                        updated,
                        tournamentId,
                        seasonId,
                        teams = teamsList.Select(t => new { t.id, t.name, t.shortName, t.logoUrl })
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams from standings");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        private async Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId)
        {
            string url = $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/standings/total";
            _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}", tournamentId, seasonId);

            return await FetchJson(url);
        }

        public async Task<object> GetTeamsByTournamentAsync(int tournamentId)
        {
            try
            {
                var teams = await _context.Teams
                    .Where(t => t.LeagueId == tournamentId)
                    .Select(t => new
                    {
                        t.TeamId,
                        t.TeamName,
                        t.ApiTeamId,
                        t.LogoUrl,
                        t.ShortName,
                        t.Founded,
                        t.National,
                        t.ClubId
                    })
                    .ToListAsync();

                return new
                {
                    status = true,
                    message = "OK",
                    data = teams
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTeamsByTournamentAsync");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }
    }
}