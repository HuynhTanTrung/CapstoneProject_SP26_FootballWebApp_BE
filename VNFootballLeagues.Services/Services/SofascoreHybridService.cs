using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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

        private async Task EnsureBrowserExistsAsync()
        {
            var browserFetcher = new BrowserFetcher();

            // Get installed browsers - this returns a list of BrowserInfo objects
            var installedBrowsers = browserFetcher.GetInstalledBrowsers();

            if (installedBrowsers.Any())
            {
                var browserInfo = installedBrowsers.First();
                _logger.LogInformation($"Browser already installed at: {browserInfo.GetExecutablePath()}");
                return;
            }

            _logger.LogInformation("Downloading browser...");

            // Use DownloadAsync without a specific revision (gets the default)
            var revision = await browserFetcher.DownloadAsync();

            _logger.LogInformation($"Browser downloaded successfully to: {revision.GetExecutablePath()}");
        }

        private async Task InitBrowser()
        {
            if (_initialized) return;

            await _lock.WaitAsync();
            try
            {
                if (_initialized) return;

                _logger.LogInformation("Launching browser...");

                // Ensure browser exists before launching
                await EnsureBrowserExistsAsync();

                var options = new LaunchOptions
                {
                    Headless = true,
                    Timeout = 60000,
                    Args = new[]
                    {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--window-size=1920,1080",
                "--disable-extensions",
                "--disable-sync",
                "--no-first-run",
                "--no-zygote",
                "--single-process"
            }
                };

                _browser = await Puppeteer.LaunchAsync(options);

                _initialized = true;

                _logger.LogInformation("Browser initialized successfully ✅");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize browser");
                throw;
            }
            finally
            {
                _lock.Release();
            }
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

        public async Task<object> SyncMatchesByRoundAsync(int apiTournamentId, int apiSeasonId)
        {
            try
            {
                var league = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);

                if (league == null)
                {
                    return new
                    {
                        status = false,
                        message = $"League with API ID {apiTournamentId} not found. Please sync leagues first.",
                        data = (object)null
                    };
                }

                var season = await _context.Seasons.FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found. Please sync seasons first.",
                        data = (object)null
                    };
                }

                var roundsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/rounds";
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
                int skipped = 0;
                var processedMatches = new HashSet<int>();

                foreach (var r in rounds.EnumerateArray())
                {
                    int round = r.GetProperty("round").GetInt32();

                    var url = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/events/round/{round}";
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

                        int? homeApiTeamId = null;
                        int? awayApiTeamId = null;
                        int? homeTeamId = null;
                        int? awayTeamId = null;

                        if (ev.TryGetProperty("homeTeam", out var homeTeam))
                        {
                            homeApiTeamId = homeTeam.GetProperty("id").GetInt32();
                        }

                        if (ev.TryGetProperty("awayTeam", out var awayTeam))
                        {
                            awayApiTeamId = awayTeam.GetProperty("id").GetInt32();
                        }

                        if (homeApiTeamId.HasValue)
                        {
                            var hTeam = await _context.Teams
                                .FirstOrDefaultAsync(t => t.ApiTeamId == homeApiTeamId);

                            if (hTeam == null)
                            {
                                _logger.LogWarning("Home team with API ID {ApiTeamId} not found in database. Please sync teams first.", homeApiTeamId);
                                skipped++;
                                continue;
                            }
                            homeTeamId = hTeam.TeamId;
                        }

                        if (awayApiTeamId.HasValue)
                        {
                            var aTeam = await _context.Teams
                                .FirstOrDefaultAsync(t => t.ApiTeamId == awayApiTeamId);

                            if (aTeam == null)
                            {
                                _logger.LogWarning("Away team with API ID {ApiTeamId} not found in database. Please sync teams first.", awayApiTeamId);
                                skipped++;
                                continue;
                            }
                            awayTeamId = aTeam.TeamId;
                        }

                        if (existingMatch == null)
                        {
                            _context.Matches.Add(new Match
                            {
                                ApiFixtureId = apiId,
                                LeagueId = league.LeagueId,    
                                SeasonId = season.SeasonId,      
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
                            existingMatch.HomeTeamId = homeTeamId ?? existingMatch.HomeTeamId;
                            existingMatch.AwayTeamId = awayTeamId ?? existingMatch.AwayTeamId;
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
                    message = $"Inserted {added} matches, Updated {updated} matches, Skipped {skipped} matches for {league.LeagueName} {season.Year}",
                    data = new { added, updated, skipped, leagueId = league.LeagueId, seasonId = season.SeasonId }
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

        public async Task<object> SyncTeamsFromStandingsAsync(int apiTournamentId, int apiSeasonId)
        {
            try
            {
                var league = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);

                if (league == null)
                {
                    return new
                    {
                        status = false,
                        message = $"League with API ID {apiTournamentId} not found. Please sync leagues first.",
                        data = (object)null
                    };
                }

                var teamsList = new List<(int id, string name, string shortName, string logoUrl)>();

                bool fromMatches = false;
                string standingsJson = null;
                try
                {
                    standingsJson = await GetTournamentStandingsAsync(apiTournamentId, apiSeasonId);
                }
                catch
                {
                    fromMatches = true;
                }

                if (!fromMatches && standingsJson != null)
                {
                    using var doc = JsonDocument.Parse(standingsJson);
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
                                        var shortName = team.TryGetProperty("shortName", out var sp) ? sp.GetString() : null;
                                        string logoUrl = null;
                                        if (team.TryGetProperty("image", out var img)) logoUrl = img.GetString();
                                        else if (team.TryGetProperty("logo", out var logo)) logoUrl = logo.GetString();
                                        teamsList.Add((id, name, shortName, logoUrl));
                                    }
                                }
                            }
                        }
                    }
                    if (teamsList.Count == 0) fromMatches = true;
                }

                if (fromMatches)
                {
                    var seen = new HashSet<int>();
                    for (int page = 0; page <= 4; page++)
                    {
                        string matchesJson;
                        try
                        {
                            matchesJson = await FetchJson(
                                $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/events/last/{page}");
                        }
                        catch { break; }

                        using var mdoc = JsonDocument.Parse(matchesJson);
                        if (!mdoc.RootElement.TryGetProperty("events", out var events)) break;
                        var arr = events.EnumerateArray().ToList();
                        if (arr.Count == 0) break;

                        foreach (var ev in arr)
                        {
                            foreach (var side in new[] { "homeTeam", "awayTeam" })
                            {
                                if (!ev.TryGetProperty(side, out var t)) continue;
                                var id = t.GetProperty("id").GetInt32();
                                if (!seen.Add(id)) continue;
                                var name = t.GetProperty("name").GetString();
                                var shortName = t.TryGetProperty("shortName", out var sp) ? sp.GetString() : null;
                                teamsList.Add((id, name, shortName, null));
                            }
                        }
                    }
                }

                if (teamsList.Count == 0)
                {
                    return new { status = false, message = "No teams found", data = (object)null };
                }

                _logger.LogInformation("Found {Count} teams for tournament {TournamentId}, season {SeasonId} (fromMatches={FromMatches})",
                    teamsList.Count, apiTournamentId, apiSeasonId, fromMatches);

                int added = 0, updated = 0;
                int internalLeagueId = league.LeagueId;

                foreach (var teamData in teamsList)
                {
                    var logoUrl = teamData.logoUrl ?? $"https://api.sofascore.app/api/v1/team/{teamData.id}/image";
                    var existingTeam = await _context.Teams.FirstOrDefaultAsync(t => t.ApiTeamId == teamData.id);

                    int? stadiumId = existingTeam?.StadiumId;
                    int? founded = existingTeam?.Founded;
                    try
                    {
                        var detailJson = await FetchJson($"https://www.sofascore.com/api/v1/team/{teamData.id}");
                        using var dd = JsonDocument.Parse(detailJson);
                        if (dd.RootElement.TryGetProperty("team", out var te))
                        {
                            try
                            {
                                if (te.TryGetProperty("venue", out var venue))
                                {
                                    var venueName = venue.TryGetProperty("name", out var vn) ? vn.GetString() : null;
                                    var venueApiId = venue.TryGetProperty("id", out var vid) ? (int?)vid.GetInt32() : null;
                                    var venueCity = venue.TryGetProperty("city", out var vc) && vc.TryGetProperty("name", out var vcn) ? vcn.GetString() : null;
                                    var venueCapacity = venue.TryGetProperty("capacity", out var vcap) ? (int?)vcap.GetInt32() : null;
                                    if (venueName != null)
                                    {
                                        Stadium stadium = null;
                                        if (venueApiId.HasValue)
                                            stadium = await _context.Stadiums.FirstOrDefaultAsync(s => s.ApiVenueId == venueApiId);
                                        if (stadium == null)
                                            stadium = await _context.Stadiums.FirstOrDefaultAsync(s => s.StadiumName == venueName);
                                        if (stadium == null)
                                        {
                                            stadium = new Stadium { StadiumName = venueName, ApiVenueId = venueApiId, City = venueCity, Capacity = venueCapacity };
                                            _context.Stadiums.Add(stadium);
                                            await _context.SaveChangesAsync();
                                        }
                                        else
                                        {
                                            bool sc = false;
                                            if (stadium.ApiVenueId == null && venueApiId != null) { stadium.ApiVenueId = venueApiId; sc = true; }
                                            if (stadium.City == null && venueCity != null) { stadium.City = venueCity; sc = true; }
                                            if (stadium.Capacity == null && venueCapacity != null) { stadium.Capacity = venueCapacity; sc = true; }
                                            if (sc) await _context.SaveChangesAsync();
                                        }
                                        stadiumId = stadium.StadiumId;
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.LogWarning("Stadium error for team {Id}: {Msg}", teamData.id, ex.InnerException?.Message ?? ex.Message); _context.ChangeTracker.Clear(); }

                            try
                            {
                                if (te.TryGetProperty("foundationDateTimestamp", out var fts))
                                    founded = DateTimeOffset.FromUnixTimeSeconds(fts.GetInt64()).Year;
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex) { _logger.LogWarning("Could not fetch detail for team {Id} ({Name}): {Msg}", teamData.id, teamData.name, ex.InnerException?.Message ?? ex.Message); }

                    if (existingTeam == null)
                    {
                        _context.Teams.Add(new Team
                        {
                            TeamName = teamData.name,
                            ClubId = 1,
                            ApiTeamId = teamData.id,
                            LogoUrl = logoUrl,
                            ShortName = teamData.shortName,
                            Founded = founded,
                            National = false,
                            LeagueId = internalLeagueId,
                            StadiumId = stadiumId
                        });
                        added++;
                    }
                    else
                    {
                        existingTeam.TeamName = teamData.name;
                        existingTeam.LogoUrl = logoUrl;
                        existingTeam.ShortName = teamData.shortName ?? existingTeam.ShortName;
                        existingTeam.LeagueId = internalLeagueId;
                        if (stadiumId.HasValue) existingTeam.StadiumId = stadiumId;
                        if (founded.HasValue) existingTeam.Founded = founded;
                        _context.Teams.Update(existingTeam);
                        updated++;
                    }

                    await _context.SaveChangesAsync();
                }

                return new
                {
                    status = true,
                    message = $"Inserted {added} teams, Updated {updated} teams for {league.LeagueName}",
                    data = new
                    {
                        added,
                        updated,
                        apiTournamentId,
                        apiSeasonId,
                        leagueId = league.LeagueId,
                        teams = teamsList.Select(t => new { t.id, t.name, t.shortName, t.logoUrl })
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams");
                return new { status = false, message = ex.Message, data = ex.StackTrace };
            }
        }

        private async Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId)
        {
            string url = $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/standings/total";
            _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}", tournamentId, seasonId);

            return await FetchJson(url);
        }

        public async Task<object> SyncTeamPlayersAsync(int sofascoreTeamId)
        {
            try
            {
                var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.ApiTeamId == sofascoreTeamId);
                if (team == null)
                    return new { status = false, message = $"Team with Sofascore ID {sofascoreTeamId} not found in DB. Sync team first." };

                int teamId = team.TeamId;

                string playersJson = await FetchJson($"https://www.sofascore.com/api/v1/team/{sofascoreTeamId}/players");
                using var doc = JsonDocument.Parse(playersJson);

                if (!doc.RootElement.TryGetProperty("players", out var playersEl))
                    return new { status = false, message = "No players array in response" };

                int added = 0, updated = 0, skipped = 0;

                var playerDataList = new List<(int apiId, string name, string shortName, string position, int? number,
                    string nationality, string photoUrl, int? age, DateOnly? dob, decimal? height)>();

                foreach (var item in playersEl.EnumerateArray())
                {
                    if (!item.TryGetProperty("player", out var p)) continue;
                    var apiId = p.GetProperty("id").GetInt32();
                    var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var shortName = p.TryGetProperty("shortName", out var sn) ? sn.GetString() : null;
                    var position = p.TryGetProperty("position", out var pos) ? pos.GetString() : null;
                    var number = item.TryGetProperty("shirtNumber", out var num) ? (int?)num.GetInt32() : null;
                    var nationality = p.TryGetProperty("country", out var country) && country.TryGetProperty("name", out var cn) ? cn.GetString() : null;
                    var photoUrl = $"https://api.sofascore.app/api/v1/player/{apiId}/image";
                    int? age = p.TryGetProperty("age", out var ageEl) ? (int?)ageEl.GetInt32() : null;
                    DateOnly? dob = null;
                    if (p.TryGetProperty("dateOfBirthTimestamp", out var dobTs))
                        dob = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(dobTs.GetInt64()).DateTime);
                    decimal? height = p.TryGetProperty("height", out var h) ? (decimal?)h.GetDecimal() : null;
                    playerDataList.Add((apiId, name, shortName, position, number, nationality, photoUrl, age, dob, height));
                }

                foreach (var pd in playerDataList)
                {
                    try
                    {
                        var existing = await _context.Players.AsNoTracking().FirstOrDefaultAsync(pl => pl.ApiPlayerId == pd.apiId);
                        if (existing == null)
                        {
                            _context.Players.Add(new Player
                            {
                                ApiPlayerId = pd.apiId,
                                FullName = pd.name ?? pd.shortName ?? $"Player {pd.apiId}",
                                FirstName = pd.name?.Split(' ').FirstOrDefault(),
                                LastName = pd.name?.Split(' ').Skip(1).LastOrDefault(),
                                Position = pd.position,
                                Number = pd.number,
                                Nationality = pd.nationality,
                                PhotoUrl = pd.photoUrl,
                                Age = pd.age,
                                DateOfBirth = pd.dob,
                                HeightCm = pd.height,
                                TeamId = teamId,
                                IsInjured = false,
                            });
                            await _context.SaveChangesAsync();
                            added++;
                        }
                        else
                        {
                            await _context.Players
                                .Where(pl => pl.PlayerId == existing.PlayerId)
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(pl => pl.FullName, pd.name ?? existing.FullName)
                                    .SetProperty(pl => pl.Position, pd.position ?? existing.Position)
                                    .SetProperty(pl => pl.Number, pd.number ?? existing.Number)
                                    .SetProperty(pl => pl.Nationality, pd.nationality ?? existing.Nationality)
                                    .SetProperty(pl => pl.PhotoUrl, pd.photoUrl)
                                    .SetProperty(pl => pl.Age, pd.age ?? existing.Age)
                                    .SetProperty(pl => pl.DateOfBirth, pd.dob ?? existing.DateOfBirth)
                                    .SetProperty(pl => pl.HeightCm, pd.height ?? existing.HeightCm)
                                    .SetProperty(pl => pl.TeamId, teamId));
                            updated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Skip player {Id} ({Name}): {Err}", pd.apiId, pd.name, ex.InnerException?.Message ?? ex.Message);
                        _context.ChangeTracker.Clear();
                        skipped++;
                    }
                }

                return new
                {
                    status = true,
                    message = $"Players: {added} added, {updated} updated, {skipped} skipped",
                    data = new { added, updated, skipped, teamId, sofascoreTeamId }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing players for team {Id}", sofascoreTeamId);
                return new { status = false, message = ex.Message, data = ex.StackTrace };
            }
        }

        public async Task<object> SyncAllTeamPlayersAsync(int tournamentId, int seasonId)
        {
            try
            {
                var standingsJson = await GetTournamentStandingsAsync(tournamentId, seasonId);
                using var doc = JsonDocument.Parse(standingsJson);

                var sofascoreTeamIds = new List<int>();
                if (doc.RootElement.TryGetProperty("standings", out var standings))
                {
                    foreach (var group in standings.EnumerateArray())
                    {
                        if (!group.TryGetProperty("rows", out var rows)) continue;
                        foreach (var row in rows.EnumerateArray())
                        {
                            if (row.TryGetProperty("team", out var t))
                                sofascoreTeamIds.Add(t.GetProperty("id").GetInt32());
                        }
                    }
                }

                if (sofascoreTeamIds.Count == 0)
                    return new { status = false, message = "No teams found in standings" };

                int totalAdded = 0, totalUpdated = 0, totalSkipped = 0;
                var results = new List<object>();

                foreach (var sofaId in sofascoreTeamIds)
                {
                    var result = await SyncTeamPlayersAsync(sofaId);
                    var resultJson = JsonSerializer.Serialize(result);
                    using var rd = JsonDocument.Parse(resultJson);
                    var root = rd.RootElement;
                    bool ok = root.TryGetProperty("status", out var st) && st.GetBoolean();
                    if (ok && root.TryGetProperty("data", out var data))
                    {
                        totalAdded += data.TryGetProperty("added", out var a) ? a.GetInt32() : 0;
                        totalUpdated += data.TryGetProperty("updated", out var u) ? u.GetInt32() : 0;
                        totalSkipped += data.TryGetProperty("skipped", out var sk) ? sk.GetInt32() : 0;
                    }
                    results.Add(new { sofascoreTeamId = sofaId, result });
                    await Task.Delay(500);
                }

                return new
                {
                    status = true,
                    message = $"Synced {sofascoreTeamIds.Count} teams — {totalAdded} added, {totalUpdated} updated, {totalSkipped} skipped",
                    data = new { totalAdded, totalUpdated, totalSkipped, teams = results }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncAllTeamPlayersAsync");
                return new { status = false, message = ex.Message, data = ex.StackTrace };
            }
        }

        public async Task<object> SyncAllPlayerStatisticsAsync(int apiTournamentId, int apiSeasonId)
        {
            try
            {
                var league = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);

                if (league == null)
                {
                    return new
                    {
                        status = false,
                        message = $"League with API ID {apiTournamentId} not found. Please sync leagues first.",
                        data = (object)null
                    };
                }

                var season = await _context.Seasons.FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found. Please sync seasons first.",
                        data = (object)null
                    };
                }

                var standingsJson = await GetTournamentStandingsAsync(apiTournamentId, apiSeasonId);
                using var standingsDoc = JsonDocument.Parse(standingsJson);

                var sofascoreTeamIds = new List<int>();
                if (standingsDoc.RootElement.TryGetProperty("standings", out var standings))
                {
                    foreach (var group in standings.EnumerateArray())
                    {
                        if (!group.TryGetProperty("rows", out var rows)) continue;
                        foreach (var row in rows.EnumerateArray())
                        {
                            if (row.TryGetProperty("team", out var t))
                                sofascoreTeamIds.Add(t.GetProperty("id").GetInt32());
                        }
                    }
                }

                if (sofascoreTeamIds.Count == 0)
                    return new { status = false, message = "No teams found in standings" };

                int added = 0, updated = 0, skipped = 0, errors = 0;

                foreach (var sofaTeamId in sofascoreTeamIds)
                {
                    var team = await _context.Teams.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.ApiTeamId == sofaTeamId);
                    if (team == null) { skipped++; continue; }

                    var teamPlayers = await _context.Players.AsNoTracking()
                        .Where(p => p.TeamId == team.TeamId)
                        .Select(p => new { p.PlayerId, p.ApiPlayerId })
                        .ToListAsync();

                    foreach (var player in teamPlayers)
                    {
                        if (player.ApiPlayerId == null) { skipped++; continue; }
                        try
                        {
                            var statsJson = await FetchJson(
                                $"https://www.sofascore.com/api/v1/player/{player.ApiPlayerId}/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/statistics/overall");
                            using var doc = JsonDocument.Parse(statsJson);

                            if (!doc.RootElement.TryGetProperty("statistics", out var s))
                            { skipped++; continue; }

                            int? goals = s.TryGetProperty("goals", out var g) ? (int?)g.GetInt32() : null;
                            int? assists = s.TryGetProperty("assists", out var a) ? (int?)a.GetInt32() : null;
                            int? appearances = s.TryGetProperty("appearances", out var ap) ? (int?)ap.GetInt32() : null;
                            int? minutesPlayed = s.TryGetProperty("minutesPlayed", out var mp) ? (int?)mp.GetInt32() : null;
                            int? yellowCards = s.TryGetProperty("yellowCards", out var yc) ? (int?)yc.GetInt32() : null;
                            int? redCards = s.TryGetProperty("redCards", out var rc) ? (int?)rc.GetInt32() : null;
                            decimal? rating = s.TryGetProperty("rating", out var r) ? (decimal?)r.GetDecimal() : null;
                            int? shotsTotal = s.TryGetProperty("totalShots", out var st) ? (int?)st.GetInt32() : null;
                            int? shotsOnTarget = s.TryGetProperty("shotsOnTarget", out var sot) ? (int?)sot.GetInt32() : null;
                            int? passesTotal = s.TryGetProperty("totalPasses", out var pt) ? (int?)pt.GetInt32() : null;
                            int? passesKey = s.TryGetProperty("keyPasses", out var kp) ? (int?)kp.GetInt32() : null;
                            decimal? passesAccuracy = s.TryGetProperty("accuratePasses", out var pac) ? (decimal?)pac.GetDecimal() : null;
                            int? dribblesAttempted = s.TryGetProperty("totalDribbleAttempts", out var da) ? (int?)da.GetInt32() : null;
                            int? dribblesSuccess = s.TryGetProperty("successfulDribbles", out var ds) ? (int?)ds.GetInt32() : null;
                            int? tackles = s.TryGetProperty("tackles", out var tk) ? (int?)tk.GetInt32() : null;
                            int? interceptions = s.TryGetProperty("interceptions", out var ic) ? (int?)ic.GetInt32() : null;
                            int? foulsCommitted = s.TryGetProperty("fouls", out var fc) ? (int?)fc.GetInt32() : null;
                            int? penaltiesScored = s.TryGetProperty("penaltyGoals", out var pg) ? (int?)pg.GetInt32() : null;
                            int? lineups = s.TryGetProperty("matchesStarted", out var ls) ? (int?)ls.GetInt32() : null;
                            int? subsIn = s.TryGetProperty("substitutionsIn", out var si) ? (int?)si.GetInt32() : null;
                            int? subsOut = s.TryGetProperty("substitutionsOut", out var so) ? (int?)so.GetInt32() : null;

                            var existing = await _context.PlayerSeasonStatistics
                                .FirstOrDefaultAsync(x =>
                                    x.PlayerId == player.PlayerId &&
                                    x.SeasonId == season.SeasonId &&
                                    x.LeagueId == league.LeagueId);

                            if (existing == null)
                            {
                                _context.PlayerSeasonStatistics.Add(new PlayerSeasonStatistic
                                {
                                    PlayerId = player.PlayerId,
                                    TeamId = team.TeamId,
                                    LeagueId = league.LeagueId,
                                    SeasonId = season.SeasonId,
                                    Appearances = appearances,
                                    Lineups = lineups,
                                    Minutes = minutesPlayed,
                                    Goals = goals,
                                    Assists = assists,
                                    YellowCards = yellowCards,
                                    RedCards = redCards,
                                    Rating = rating,
                                    SubstitutionsIn = subsIn,
                                    SubstitutionsOut = subsOut,
                                    ShotsTotal = shotsTotal,
                                    ShotsOnTarget = shotsOnTarget,
                                    PassesTotal = passesTotal,
                                    PassesKey = passesKey,
                                    PassesAccuracy = passesAccuracy,
                                    DribblesAttempted = dribblesAttempted,
                                    DribblesSuccess = dribblesSuccess,
                                    Tackles = tackles,
                                    Interceptions = interceptions,
                                    FoulsCommitted = foulsCommitted,
                                    PenaltiesScored = penaltiesScored,
                                });
                                added++;
                            }
                            else
                            {
                                existing.TeamId = team.TeamId;
                                existing.Appearances = appearances;
                                existing.Lineups = lineups;
                                existing.Minutes = minutesPlayed;
                                existing.Goals = goals;
                                existing.Assists = assists;
                                existing.YellowCards = yellowCards;
                                existing.RedCards = redCards;
                                existing.Rating = rating;
                                existing.SubstitutionsIn = subsIn;
                                existing.SubstitutionsOut = subsOut;
                                existing.ShotsTotal = shotsTotal;
                                existing.ShotsOnTarget = shotsOnTarget;
                                existing.PassesTotal = passesTotal;
                                existing.PassesKey = passesKey;
                                existing.PassesAccuracy = passesAccuracy;
                                existing.DribblesAttempted = dribblesAttempted;
                                existing.DribblesSuccess = dribblesSuccess;
                                existing.Tackles = tackles;
                                existing.Interceptions = interceptions;
                                existing.FoulsCommitted = foulsCommitted;
                                existing.PenaltiesScored = penaltiesScored;
                                updated++;
                            }

                            await _context.SaveChangesAsync();
                            await Task.Delay(300);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed stats for player {Id}: {Msg}", player.ApiPlayerId, ex.Message);
                            errors++;
                        }
                    }
                }

                return new
                {
                    status = true,
                    message = $"Player stats synced for {league.LeagueName} {season.Year} — {added} added, {updated} updated, {skipped} skipped, {errors} errors",
                    data = new { added, updated, skipped, errors, leagueId = league.LeagueId, seasonId = season.SeasonId }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncAllPlayerStatisticsAsync");
                return new { status = false, message = ex.Message, data = ex.StackTrace };
            }
        }

        private int GetYearFromApiSeason(int apiSeasonId)
        {
            try
            {
                return DateTime.UtcNow.Year;
            }
            catch
            {
                return DateTime.UtcNow.Year;
            }
        }

        public async Task<object> GetTeamsByTournamentAsync(int tournamentId) {
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
                _logger.LogError(ex, "Error in GetTeamsByLeagueAsync");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncVietnameseLeaguesAsync()
        {
            try
            {
                var leaguesToSync = new List<(int apiId, string name, string type)>
        {
            (626, "V-League 1", "Professional"),
            (771, "V-League 2", "Professional"),     
            (3087, "Vietnam Cup", "Cup")           
        };

                int added = 0;
                int updated = 0;

                foreach (var (apiId, name, type) in leaguesToSync)
                {
                    string logoUrl = $"https://api.sofascore.app/api/v1/unique-tournament/{apiId}/image";

                    try
                    {
                        var detailsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiId}";
                        var leagueDetailsJson = await FetchJson(detailsUrl);

                        using var doc = JsonDocument.Parse(leagueDetailsJson);
                        if (doc.RootElement.TryGetProperty("uniqueTournament", out var tournament))
                        {
                            if (tournament.TryGetProperty("image", out var img) && img.ValueKind != JsonValueKind.Null)
                            {
                                logoUrl = img.GetString();
                            }

                            _logger.LogInformation("Fetched details for league: {Name}", name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch additional details for league {ApiId}, using defaults", apiId);
                    }

                    var existingLeague = await _context.Leagues
                        .FirstOrDefaultAsync(l => l.ApiLeagueId == apiId);

                    if (existingLeague == null)
                    {
                        var newLeague = new League
                        {
                            ApiLeagueId = apiId,
                            LeagueName = name,
                            LeagueType = type,
                            LogoUrl = logoUrl
                        };

                        _context.Leagues.Add(newLeague);
                        added++;
                        _logger.LogInformation("Added league: {Name} (API ID: {ApiId})", name, apiId);
                    }
                    else
                    {
                        existingLeague.LeagueName = name;
                        existingLeague.LeagueType = type;
                        existingLeague.LogoUrl = logoUrl;

                        _context.Leagues.Update(existingLeague);
                        updated++;
                        _logger.LogInformation("Updated league: {Name} (API ID: {ApiId})", name, apiId);
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Successfully synced Vietnamese leagues: {added} added, {updated} updated",
                    data = new
                    {
                        added,
                        updated,
                        leagues = new[]
                        {
                    new { apiId = 626, name = "V-League 1", type = "Professional" },
                    new { apiId = 771, name = "V-League 2", type = "Professional" },
                    new { apiId = 3087, name = "Vietnam Cup", type = "Cup" }
                }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Vietnamese leagues");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncSeasonsByLeagueAsync(int apiTournamentId)
        {
            try
            {
                var league = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);

                if (league == null)
                {
                    return new
                    {
                        status = false,
                        message = $"League with API ID {apiTournamentId} not found. Please sync leagues first.",
                        data = (object)null
                    };
                }

                var seasonsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/seasons";
                var seasonsJson = await FetchJson(seasonsUrl);

                using var doc = JsonDocument.Parse(seasonsJson);

                if (!doc.RootElement.TryGetProperty("seasons", out var seasons))
                {
                    return new
                    {
                        status = false,
                        message = "No seasons found in response",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;

                foreach (var seasonEl in seasons.EnumerateArray())
                {
                    if (!seasonEl.TryGetProperty("id", out var idEl))
                        continue;

                    int apiSeasonId = idEl.GetInt32();

                    string seasonName = null;
                    if (seasonEl.TryGetProperty("name", out var nameEl))
                        seasonName = nameEl.GetString();

                    int? year = null;
                    if (!string.IsNullOrEmpty(seasonName))
                    {
                        var parts = seasonName.Split('/');
                        if (int.TryParse(parts[^1], out var y))
                            year = y;
                        else if (int.TryParse(seasonName, out y))
                            year = y;
                    }

                    bool isCurrent = false;
                    if (seasonEl.TryGetProperty("current", out var currentEl))
                        isCurrent = currentEl.GetBoolean();

                    var existingSeason = await _context.Seasons
                        .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

                    if (existingSeason == null)
                    {
                        existingSeason = await _context.Seasons
                            .FirstOrDefaultAsync(s => s.LeagueId == league.LeagueId && s.Year == year);
                    }

                    if (existingSeason == null)
                    {
                        var newSeason = new Season
                        {
                            LeagueId = league.LeagueId,
                            Year = year,
                            ApiSeasonId = apiSeasonId,
                        };

                        _context.Seasons.Add(newSeason);
                        added++;
                        _logger.LogInformation("Added season: {SeasonName} (Year: {Year}, API ID: {ApiId}) for league {LeagueName}",
                            seasonName, year, apiSeasonId, league.LeagueName);
                    }
                    else
                    {
                        existingSeason.Year = year ?? existingSeason.Year;
                        existingSeason.ApiSeasonId = apiSeasonId;

                        _context.Seasons.Update(existingSeason);
                        updated++;
                        _logger.LogInformation("Updated season: {SeasonName} (Year: {Year}, API ID: {ApiId}) for league {LeagueName}",
                            seasonName, year, apiSeasonId, league.LeagueName);
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Synced seasons for {league.LeagueName}: {added} added, {updated} updated",
                    data = new
                    {
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        apiTournamentId = apiTournamentId,
                        added,
                        updated
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing seasons for tournament {ApiId}", apiTournamentId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncPlayerMatchStatisticsAsync(int apiFixtureId, int apiPlayerId)
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
                        message = $"Match with API ID {apiFixtureId} not found in database",
                        data = (object)null
                    };
                }

                var player = await _context.Players
                    .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayerId);

                if (player == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Player with API ID {apiPlayerId} not found in database",
                        data = (object)null
                    };
                }

                var statsUrl = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/player/{apiPlayerId}/statistics";
                string statsJson;

                try
                {
                    statsJson = await FetchJson(statsUrl);
                }
                catch (Exception ex)
                {
                    return new
                    {
                        status = false,
                        message = $"Could not fetch statistics: {ex.Message}",
                        data = (object)null
                    };
                }

                using var doc = JsonDocument.Parse(statsJson);

                var playerStats = ExtractPlayerMatchStatisticsFromJson(doc.RootElement, match, player);

                if (playerStats == null)
                {
                    return new
                    {
                        status = false,
                        message = "No statistics found for this player in this match",
                        data = (object)null
                    };
                }

                var existingStats = await _context.PlayerMatchStatistics
                    .FirstOrDefaultAsync(ps => ps.MatchId == match.MatchId &&
                                               ps.PlayerId == player.PlayerId);

                if (existingStats == null)
                {
                    _context.PlayerMatchStatistics.Add(playerStats);
                    await _context.SaveChangesAsync();

                    return new
                    {
                        status = true,
                        message = $"Added match statistics for player {player.FullName} in match {match.MatchId}",
                        data = new { added = true, playerMatchStatId = playerStats.PlayerMatchStatId }
                    };
                }
                else
                {
                    UpdateExistingPlayerMatchStatistics(existingStats, playerStats);
                    _context.PlayerMatchStatistics.Update(existingStats);
                    await _context.SaveChangesAsync();

                    return new
                    {
                        status = true,
                        message = $"Updated match statistics for player {player.FullName} in match {match.MatchId}",
                        data = new { updated = true, playerMatchStatId = existingStats.PlayerMatchStatId }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing player match statistics for match {ApiFixtureId}, player {ApiPlayerId}",
                    apiFixtureId, apiPlayerId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        private PlayerMatchStatistic ExtractPlayerMatchStatisticsFromJson(JsonElement root, Match match, Player player)
        {
            var playerStats = new PlayerMatchStatistic
            {
                MatchId = match.MatchId,
                PlayerId = player.PlayerId,
                TeamId = player.TeamId
            };

            if (root.TryGetProperty("statistics", out var statisticsElement))
            {
                if (statisticsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stat in statisticsElement.EnumerateArray())
                    {
                        if (stat.TryGetProperty("name", out var name) && stat.TryGetProperty("value", out var value))
                        {
                            MapStatisticToPlayerMatchStats(playerStats, name.GetString(), value);
                        }
                    }
                }
                else if (statisticsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var stat in statisticsElement.EnumerateObject())
                    {
                        MapStatisticToPlayerMatchStats(playerStats, stat.Name, stat.Value);
                    }
                }
            }

            var directProperties = new[] { "minutesPlayed", "goals", "assists", "totalShots", "shotsOnTarget",
        "totalPasses", "tackles", "yellowCards", "redCards", "rating", "offsides", "keyPasses",
        "successfulDribbles", "interceptions", "clearances", "fouls", "wasFouled", "penaltyGoals",
        "expectedGoals", "accuratePasses", "totalDribbleAttempts", "duelsWon", "duelsTotal",
        "tacklesWon", "blocks", "penaltyMissed", "penaltyWon", "penaltyCommitted" };

            foreach (var propName in directProperties)
            {
                if (root.TryGetProperty(propName, out var value))
                {
                    MapStatisticToPlayerMatchStats(playerStats, propName, value);
                }
            }

            if (root.TryGetProperty("accuratePasses", out var accuratePasses) &&
                root.TryGetProperty("totalPasses", out var totalPasses2) &&
                totalPasses2.ValueKind != JsonValueKind.Null &&
                totalPasses2.GetInt32() > 0)
            {
                playerStats.PassesAccuracy = (int)Math.Round((double)accuratePasses.GetInt32() / totalPasses2.GetInt32() * 100);
            }

            if (playerStats.Minutes == null && playerStats.Goals == null &&
                playerStats.Assists == null && playerStats.Rating == null)
            {
                return null;
            }

            return playerStats;
        }

        private void MapStatisticToPlayerMatchStats(PlayerMatchStatistic stats, string statName, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
                return;

            switch (statName?.ToLower())
            {
                case "minutesplayed":
                case "minutes":
                    stats.Minutes = value.GetInt32();
                    break;
                case "goals":
                    stats.Goals = value.GetInt32();
                    break;
                case "assists":
                    stats.Assists = value.GetInt32();
                    break;
                case "totalshots":
                case "shots":
                    stats.Shots = value.GetInt32();
                    break;
                case "shotsontarget":
                    stats.ShotsOnTarget = value.GetInt32();
                    break;
                case "totalpasses":
                case "passes":
                    stats.Passes = value.GetInt32();
                    break;
                case "tackles":
                    stats.Tackles = value.GetInt32();
                    break;
                case "yellowcards":
                    stats.YellowCards = value.GetInt32();
                    break;
                case "redcards":
                    stats.RedCards = value.GetInt32();
                    break;
                case "rating":
                    if (value.ValueKind == JsonValueKind.Number)
                        stats.Rating = (decimal)value.GetDouble();
                    break;
                case "offsides":
                    stats.Offsides = value.GetInt32();
                    break;
                case "keypasses":
                    stats.PassesKey = value.GetInt32();
                    break;
                case "successfuldribbles":
                    stats.DribblesSuccess = value.GetInt32();
                    break;
                case "interceptions":
                    stats.Interceptions = value.GetInt32();
                    break;
                case "clearances":
                    stats.Clearances = value.GetInt32();
                    break;
                case "fouls":
                    stats.FoulsCommitted = value.GetInt32();
                    break;
                case "wasfouled":
                    stats.FoulsDrawn = value.GetInt32();
                    break;
                case "penaltygoals":
                    stats.PenaltiesScored = value.GetInt32();
                    break;
                case "expectedgoals":
                case "xG":
                    if (value.ValueKind == JsonValueKind.Number)
                        stats.ExpectedGoals = (decimal)value.GetDouble();
                    break;
            }
        }

        private void UpdateExistingPlayerMatchStatistics(PlayerMatchStatistic existing, PlayerMatchStatistic newStats)
        {
            if (newStats.Minutes.HasValue) existing.Minutes = newStats.Minutes;
            if (newStats.Goals.HasValue) existing.Goals = newStats.Goals;
            if (newStats.Assists.HasValue) existing.Assists = newStats.Assists;
            if (newStats.Shots.HasValue) existing.Shots = newStats.Shots;
            if (newStats.ShotsOnTarget.HasValue) existing.ShotsOnTarget = newStats.ShotsOnTarget;
            if (newStats.Passes.HasValue) existing.Passes = newStats.Passes;
            if (newStats.Tackles.HasValue) existing.Tackles = newStats.Tackles;
            if (newStats.YellowCards.HasValue) existing.YellowCards = newStats.YellowCards;
            if (newStats.RedCards.HasValue) existing.RedCards = newStats.RedCards;
            if (newStats.Rating.HasValue) existing.Rating = newStats.Rating;
            if (newStats.Offsides.HasValue) existing.Offsides = newStats.Offsides;
            if (newStats.PassesAccuracy.HasValue) existing.PassesAccuracy = newStats.PassesAccuracy;
            if (newStats.PassesKey.HasValue) existing.PassesKey = newStats.PassesKey;
            if (newStats.DribblesAttempted.HasValue) existing.DribblesAttempted = newStats.DribblesAttempted;
            if (newStats.DribblesSuccess.HasValue) existing.DribblesSuccess = newStats.DribblesSuccess;
            if (newStats.DuelsWon.HasValue) existing.DuelsWon = newStats.DuelsWon;
            if (newStats.DuelsTotal.HasValue) existing.DuelsTotal = newStats.DuelsTotal;
            if (newStats.TacklesWon.HasValue) existing.TacklesWon = newStats.TacklesWon;
            if (newStats.Blocks.HasValue) existing.Blocks = newStats.Blocks;
            if (newStats.Interceptions.HasValue) existing.Interceptions = newStats.Interceptions;
            if (newStats.Clearances.HasValue) existing.Clearances = newStats.Clearances;
            if (newStats.FoulsDrawn.HasValue) existing.FoulsDrawn = newStats.FoulsDrawn;
            if (newStats.FoulsCommitted.HasValue) existing.FoulsCommitted = newStats.FoulsCommitted;
            if (newStats.PenaltiesWon.HasValue) existing.PenaltiesWon = newStats.PenaltiesWon;
            if (newStats.PenaltiesCommitted.HasValue) existing.PenaltiesCommitted = newStats.PenaltiesCommitted;
            if (newStats.PenaltiesScored.HasValue) existing.PenaltiesScored = newStats.PenaltiesScored;
            if (newStats.PenaltiesMissed.HasValue) existing.PenaltiesMissed = newStats.PenaltiesMissed;
            if (newStats.ExpectedGoals.HasValue) existing.ExpectedGoals = newStats.ExpectedGoals;
        }


    }
}