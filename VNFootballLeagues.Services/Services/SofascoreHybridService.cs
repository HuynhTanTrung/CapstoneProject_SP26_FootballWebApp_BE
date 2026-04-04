using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Net;
using System.Text.Json;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
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

            var installedBrowsers = browserFetcher.GetInstalledBrowsers();

            if (installedBrowsers.Any())
            {
                var browserInfo = installedBrowsers.First();
                _logger.LogInformation($"Browser already installed at: {browserInfo.GetExecutablePath()}");
                return;
            }

            _logger.LogInformation("Downloading browser...");

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

                page.DefaultTimeout = 30000;

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

                if (response.Status == HttpStatusCode.NotFound)
                {
                    // 404 is definitive - no point retrying
                    throw new HttpRequestException($"HTTP Error: {response.Status}");
                }

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
            catch (Exception ex) when (retryCount > 0 && ex is not HttpRequestException hre || (retryCount > 0 && ex is HttpRequestException httpEx && !httpEx.Message.Contains("NotFound")))
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

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

                // For knockout tournaments (Cup), use page-based fetch instead of round-based
                // Detect by league type or by checking if rounds have cupRoundType
                bool usePageBased = league.LeagueType?.ToLower().Contains("cup") == true
                    || league.LeagueType?.ToLower().Contains("knockout") == true
                    || rounds.EnumerateArray().Any(r => r.TryGetProperty("cupRoundType", out _));

                if (usePageBased)
                {
                    // Fetch all matches via last/next pages
                    var allEvents = new List<JsonElement>();
                    for (int page = 0; page <= 5; page++)
                    {
                        try
                        {
                            var lastUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/events/last/{page}";
                            var lastJson = await FetchJson(lastUrl);
                            using var lastDoc = JsonDocument.Parse(lastJson);
                            if (lastDoc.RootElement.TryGetProperty("events", out var evs))
                                allEvents.AddRange(evs.EnumerateArray().Select(e => e.Clone()));
                            if (lastDoc.RootElement.TryGetProperty("hasNextPage", out var hnp) && !hnp.GetBoolean()) break;
                        }
                        catch { break; }
                    }
                    for (int page = 0; page <= 2; page++)
                    {
                        try
                        {
                            var nextUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/events/next/{page}";
                            var nextJson = await FetchJson(nextUrl);
                            using var nextDoc = JsonDocument.Parse(nextJson);
                            if (nextDoc.RootElement.TryGetProperty("events", out var evs))
                                allEvents.AddRange(evs.EnumerateArray().Select(e => e.Clone()));
                            if (nextDoc.RootElement.TryGetProperty("hasNextPage", out var hnp) && !hnp.GetBoolean()) break;
                        }
                        catch { break; }
                    }

                    foreach (var ev in allEvents)
                    {
                        var apiId = ev.GetProperty("id").GetInt32();
                        if (processedMatches.Contains(apiId)) continue;
                        processedMatches.Add(apiId);
                        var result = await UpsertMatchFromEvent(ev, league, season);
                        if (result == 1) added++;
                        else if (result == 2) updated++;
                    }
                }
                else
                {
                    foreach (var r in rounds.EnumerateArray())
                    {
                    int round = r.TryGetProperty("round", out var rp) ? rp.GetInt32() : 0;
                    if (round == 0) { skipped++; continue; }

                    var url = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/events/round/{round}";

                    string json;
                    try
                    {
                        json = await FetchJson(url);
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("NotFound") || ex.Message.Contains("404"))
                    {
                        _logger.LogWarning("Round {Round} not found for tournament {TournamentId} season {SeasonId}",
                            round, apiTournamentId, apiSeasonId);
                        skipped++;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to fetch round {Round}: {Message}", round, ex.Message);
                        skipped++;
                        continue;
                    }

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
                                _logger.LogWarning("Home team with API ID {ApiTeamId} not found", homeApiTeamId);
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
                                _logger.LogWarning("Away team with API ID {ApiTeamId} not found", awayApiTeamId);
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
                    } // end foreach round
                } // end else

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Inserted {added} matches, Updated {updated} matches, Skipped {skipped} rounds for {league.LeagueName} {season.Year}",
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
                    // Only process the "ALL" period (first period) — merge all groups into one row per team
                    var allPeriod = statistics.EnumerateArray()
                        .FirstOrDefault(p => p.TryGetProperty("period", out var per) && per.GetString() == "ALL");

                    // Fallback to first period if "ALL" not found
                    if (allPeriod.ValueKind == JsonValueKind.Undefined)
                        allPeriod = statistics.EnumerateArray().FirstOrDefault();

                    if (allPeriod.ValueKind != JsonValueKind.Undefined)
                    {
                        var result = await ProcessStatisticsPeriod(allPeriod, match.MatchId);
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

        public async Task<List<Lineup>> GetAllLineupsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);

            if (league == null)
            {
                return new List<Lineup>();
            }

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

            if (season == null)
            {
                return new List<Lineup>();
            }

            return await _context.Lineups
                .Where(l => l.Match.LeagueId == league.LeagueId && l.Match.SeasonId == season.SeasonId)
                .Include(l => l.Match)
                    .ThenInclude(m => m.HomeTeam)
                .Include(l => l.Match)
                    .ThenInclude(m => m.AwayTeam)
                .Include(l => l.Team)
                .OrderBy(l => l.Match.MatchDate)
                .ThenBy(l => l.Team.TeamName)
                .ToListAsync();
        }

        private async Task<(int added, int updated)> ProcessStatisticsPeriod(JsonElement period, int matchId)
        {
            int added = 0;
            int updated = 0;

            if (!period.TryGetProperty("groups", out var groups))
                return (added, updated);

            // Merge all groups into one stat object per team
            var homeStats = new MatchStatistic { MatchId = matchId };
            var awayStats = new MatchStatistic { MatchId = matchId };
            bool homeHasData = false;
            bool awayHasData = false;

            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("statisticsItems", out var statisticsItems)) continue;

                foreach (var stat in statisticsItems.EnumerateArray())
                {
                    string statName = stat.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrEmpty(statName)) continue;

                    string homeVal = null, awayVal = null;
                    if (stat.TryGetProperty("home", out var hv))
                        homeVal = hv.ValueKind == JsonValueKind.String ? hv.GetString()
                                : hv.ValueKind == JsonValueKind.Number ? hv.GetRawText() : null;
                    if (stat.TryGetProperty("away", out var av))
                        awayVal = av.ValueKind == JsonValueKind.String ? av.GetString()
                                : av.ValueKind == JsonValueKind.Number ? av.GetRawText() : null;

                    ApplyStatToMatchStatistic(homeStats, statName, homeVal, ref homeHasData);
                    ApplyStatToMatchStatistic(awayStats, statName, awayVal, ref awayHasData);
                }
            }

            if (homeHasData)
            {
                if (await SaveOrUpdateStatisticsAsync(homeStats, matchId, "home")) added++;
            }
            if (awayHasData)
            {
                if (await SaveOrUpdateStatisticsAsync(awayStats, matchId, "away")) added++;
            }

            return (added, updated);
        }

        private void ApplyStatToMatchStatistic(MatchStatistic stats, string statName, string value, ref bool hasData)
        {
            if (string.IsNullOrEmpty(value)) return;
            hasData = true;
            switch (statName.ToLower())
            {
                case "ball possession":       stats.Possession = ParseIntPercentage(value); break;
                case "expected goals":        stats.ExpectedGoals = ParseDecimal(value); break;
                case "total shots":           stats.Shots = ParseInt(value); break;
                case "shots on target":       stats.ShotsOnTarget = ParseInt(value); break;
                case "corner kicks":          stats.Corners = ParseInt(value); break;
                case "fouls":                 stats.Fouls = ParseInt(value); break;
                case "yellow cards":          stats.YellowCards = ParseInt(value); break;
                case "red cards":             stats.RedCards = ParseInt(value); break;
                case "offsides":              stats.Offsides = ParseInt(value); break;
                case "blocked shots":         stats.ShotsBlocked = ParseInt(value); break;
                case "shots inside box":      stats.ShotsInsideBox = ParseInt(value); break;
                case "shots outside box":     stats.ShotsOutsideBox = ParseInt(value); break;
                case "total saves":
                case "goalkeeper saves":      stats.Saves = ParseInt(value); break;
                case "interceptions":         stats.Interceptions = ParseInt(value); break;
                case "clearances":            stats.Clearances = ParseInt(value); break;
                case "total tackles":         stats.TacklesWon = ParseInt(value); break;
                case "passes":
                case "total passes":          if (stats.PassesAccuracy == null) stats.PassesAccuracy = ParseInt(value); break;
                case "accurate passes":       stats.PassesAccuracy = ParseInt(value); break;
                case "key passes":            stats.PassesKey = ParseInt(value); break;
                case "dribbles":
                case "successful dribbles":   stats.DribblesSuccess = ParseInt(value); break;
                case "dribble attempts":      stats.DribblesAttempted = ParseInt(value); break;
                case "duels won":             stats.DuelsWon = ParseInt(value); break;
                case "duels":
                case "total duels":           stats.DuelsTotal = ParseInt(value); break;
            }
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

            // Remove ALL existing rows for this match+team (handles duplicates from old syncs)
            var existingRows = await _context.MatchStatistics
                .Where(s => s.MatchId == matchId && s.TeamId == teamId)
                .ToListAsync();

            if (existingRows.Count > 0)
                _context.MatchStatistics.RemoveRange(existingRows);

            await _context.MatchStatistics.AddAsync(stats);
            return true;
        }

        // Returns 1=added, 2=updated, 0=skipped
        private async Task<int> UpsertMatchFromEvent(JsonElement ev, League league, Season season)
        {
            var apiId = ev.GetProperty("id").GetInt32();
            var matchDate = DateTimeOffset.FromUnixTimeSeconds(ev.GetProperty("startTimestamp").GetInt64()).DateTime;

            int? homeTeamId = null, awayTeamId = null;
            if (ev.TryGetProperty("homeTeam", out var ht))
            {
                var hApiId = ht.GetProperty("id").GetInt32();
                var hTeam = await _context.Teams.FirstOrDefaultAsync(t => t.ApiTeamId == hApiId);
                if (hTeam == null) return 0;
                homeTeamId = hTeam.TeamId;
            }
            if (ev.TryGetProperty("awayTeam", out var at))
            {
                var aApiId = at.GetProperty("id").GetInt32();
                var aTeam = await _context.Teams.FirstOrDefaultAsync(t => t.ApiTeamId == aApiId);
                if (aTeam == null) return 0;
                awayTeamId = aTeam.TeamId;
            }

            string roundStr = "0";
            if (ev.TryGetProperty("roundInfo", out var ri))
            {
                if (ri.TryGetProperty("name", out var rn)) roundStr = rn.GetString() ?? "0";
                else if (ri.TryGetProperty("round", out var rnum)) roundStr = rnum.GetInt32().ToString();
            }

            var existing = await _context.Matches.FirstOrDefaultAsync(x => x.ApiFixtureId == apiId);
            if (existing == null)
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
                    Round = roundStr,
                    Venue = ev.TryGetProperty("venue", out var v) && v.TryGetProperty("stadium", out var s) ? s.GetProperty("name").GetString() : null,
                });
                return 1;
            }
            else
            {
                existing.MatchDate = matchDate;
                existing.HomeGoals = SafeScore(ev, "homeScore");
                existing.AwayGoals = SafeScore(ev, "awayScore");
                existing.Status = ev.GetProperty("status").GetProperty("type").GetString();
                existing.HomeTeamId = homeTeamId ?? existing.HomeTeamId;
                existing.AwayTeamId = awayTeamId ?? existing.AwayTeamId;
                _context.Matches.Update(existing);
                return 2;
            }
        }

        private int? SafeScore(JsonElement ev, string key)
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
            return null;
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
                    string nationality, string birthCountry, string photoUrl, int? age, DateOnly? dob, decimal? height)>();

                foreach (var item in playersEl.EnumerateArray())
                {
                    if (!item.TryGetProperty("player", out var p)) continue;
                    var apiId = p.GetProperty("id").GetInt32();
                    var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var shortName = p.TryGetProperty("shortName", out var sn) ? sn.GetString() : null;
                    var position = p.TryGetProperty("position", out var pos) ? pos.GetString() : null;
                    var number = p.TryGetProperty("shirtNumber", out var num) ? (int?)num.GetInt32() : null;
                    var nationality = p.TryGetProperty("country", out var country) && country.TryGetProperty("name", out var cn) ? cn.GetString() : null;
                    var birthCountry = nationality; // same source from Sofascore
                    var photoUrl = $"https://api.sofascore.app/api/v1/player/{apiId}/image";
                    int? age = p.TryGetProperty("age", out var ageEl) ? (int?)ageEl.GetInt32() : null;
                    DateOnly? dob = null;
                    if (p.TryGetProperty("dateOfBirthTimestamp", out var dobTs))
                    {
                        dob = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(dobTs.GetInt64()).DateTime);
                        if (age == null)
                        {
                            var today = DateTime.Today;
                            var dobDate = dob.Value.ToDateTime(TimeOnly.MinValue);
                            age = today.Year - dobDate.Year;
                            if (dobDate.AddYears(age.Value) > today) age--;
                        }
                    }
                    decimal? height = p.TryGetProperty("height", out var h) ? (decimal?)h.GetDecimal() : null;
                    playerDataList.Add((apiId, name, shortName, position, number, nationality, birthCountry, photoUrl, age, dob, height));
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
                                BirthCountry = pd.birthCountry,
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
                                    .SetProperty(pl => pl.Number, pd.number)
                                    .SetProperty(pl => pl.Nationality, pd.nationality ?? existing.Nationality)
                                    .SetProperty(pl => pl.BirthCountry, pd.birthCountry ?? existing.BirthCountry)
                                    .SetProperty(pl => pl.PhotoUrl, pd.photoUrl)
                                    .SetProperty(pl => pl.Age, pd.age)
                                    .SetProperty(pl => pl.DateOfBirth, pd.dob)
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

                            // GK stats
                            int? saves = s.TryGetProperty("saves", out var sv) ? (int?)sv.GetInt32() : null;
                            int? savesInsideBox = s.TryGetProperty("savedShotsFromInsideTheBox", out var sib) ? (int?)sib.GetInt32() : null;
                            int? punches = s.TryGetProperty("punches", out var pn) ? (int?)pn.GetInt32() : null;
                            int? runsOut = s.TryGetProperty("totalKeeperSweeper", out var ro) ? (int?)ro.GetInt32() : null;
                            int? runsOutSuccessful = s.TryGetProperty("keeperSweeper", out var ros) ? (int?)ros.GetInt32() : null;
                            int? highClaims = s.TryGetProperty("goodHighClaim", out var hc) ? (int?)hc.GetInt32() : null;
                            int? goalsConceded = s.TryGetProperty("goalsConceded", out var gc) ? (int?)gc.GetInt32() : null;
                            int? penaltiesSaved = s.TryGetProperty("penaltySave", out var ps) ? (int?)ps.GetInt32() : null;
                            int? cleanSheets = s.TryGetProperty("cleanSheet", out var cs) ? (int?)cs.GetInt32() : null;
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
                                    Saves = saves,
                                    SavesInsideBox = savesInsideBox,
                                    Punches = punches,
                                    RunsOut = runsOut,
                                    RunsOutSuccessful = runsOutSuccessful,
                                    HighClaims = highClaims,
                                    GoalsConceded = goalsConceded,
                                    PenaltiesSaved = penaltiesSaved,
                                    CleanSheets = cleanSheets,
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
                                existing.Saves = saves;
                                existing.SavesInsideBox = savesInsideBox;
                                existing.Punches = punches;
                                existing.RunsOut = runsOut;
                                existing.RunsOutSuccessful = runsOutSuccessful;
                                existing.HighClaims = highClaims;
                                existing.GoalsConceded = goalsConceded;
                                existing.PenaltiesSaved = penaltiesSaved;
                                existing.CleanSheets = cleanSheets;
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

                // After syncing from Sofascore, aggregate defensive stats from match stats
                await AggregateSeasonStatsFromMatchStatsAsync(leagueId: league.LeagueId, seasonId: season.SeasonId);

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

        public async Task<object> SyncMatchStatisticsByLeagueAndSeasonAsync(int apiTournamentId, int apiSeasonId)
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found for league {league.LeagueName}. Please sync seasons first.",
                        data = (object)null
                    };
                }

                var matches = await _context.Matches
                    .Where(m => m.LeagueId == league.LeagueId &&
                               m.SeasonId == season.SeasonId &&
                               m.ApiFixtureId.HasValue &&
                               m.ApiFixtureId > 0)
                    .Select(m => new { m.MatchId, m.ApiFixtureId, m.MatchDate, m.HomeTeam, m.AwayTeam })
                    .OrderBy(m => m.MatchDate)
                    .ToListAsync();

                if (!matches.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No matches found for {league.LeagueName} season {season.Year} with valid ApiFixtureId",
                        data = (object)null
                    };
                }

                int totalMatchesProcessed = 0;
                int totalAdded = 0;
                int totalUpdated = 0;
                int totalFailed = 0;
                var matchResults = new List<object>();

                foreach (var match in matches)
                {
                    int apiFixtureId = match.ApiFixtureId.Value;

                    try
                    {
                        var statisticsUrl = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/statistics";
                        var json = await FetchJson(statisticsUrl);

                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("statistics", out var statistics) &&
                            statistics.ValueKind == JsonValueKind.Array)
                        {
                            int matchAdded = 0;
                            int matchUpdated = 0;

                            // Only process "ALL" period — merge all groups into one row per team
                            var allPeriod = statistics.EnumerateArray()
                                .FirstOrDefault(p => p.TryGetProperty("period", out var per) && per.GetString() == "ALL");
                            if (allPeriod.ValueKind == JsonValueKind.Undefined)
                                allPeriod = statistics.EnumerateArray().FirstOrDefault();

                            if (allPeriod.ValueKind != JsonValueKind.Undefined)
                            {
                                var result = await ProcessStatisticsPeriod(allPeriod, match.MatchId);
                                matchAdded += result.added;
                                matchUpdated += result.updated;
                            }

                            totalAdded += matchAdded;
                            totalUpdated += matchUpdated;
                            totalMatchesProcessed++;

                            matchResults.Add(new
                            {
                                matchId = match.MatchId,
                                apiFixtureId = apiFixtureId,
                                matchDate = match.MatchDate,
                                homeTeam = match.HomeTeam?.TeamName,
                                awayTeam = match.AwayTeam?.TeamName,
                                added = matchAdded,
                                updated = matchUpdated,
                                success = true
                            });

                            _logger.LogInformation("Processed match {ApiFixtureId} for {League} {Season}: +{Added} added, +{Updated} updated",
                                apiFixtureId, league.LeagueName, season.Year, matchAdded, matchUpdated);
                        }
                        else
                        {
                            totalFailed++;
                            matchResults.Add(new
                            {
                                matchId = match.MatchId,
                                apiFixtureId = apiFixtureId,
                                matchDate = match.MatchDate,
                                homeTeam = match.HomeTeam?.TeamName,
                                awayTeam = match.AwayTeam?.TeamName,
                                success = false,
                                reason = "No statistics found"
                            });
                        }

                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        totalFailed++;
                        matchResults.Add(new
                        {
                            matchId = match.MatchId,
                            apiFixtureId = apiFixtureId,
                            matchDate = match.MatchDate,
                            homeTeam = match.HomeTeam?.TeamName,
                            awayTeam = match.AwayTeam?.TeamName,
                            success = false,
                            reason = ex.Message
                        });

                        _logger.LogWarning(ex, "Failed to sync statistics for match {ApiFixtureId}", apiFixtureId);
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Synced match statistics for {league.LeagueName} {season.Year}: " +
                              $"{totalMatchesProcessed} matches processed, " +
                              $"{totalAdded} added, {totalUpdated} updated, {totalFailed} failed",
                    data = new
                    {
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        seasonId = season.SeasonId,
                        seasonYear = season.Year,
                        totalMatches = matches.Count,
                        totalMatchesProcessed,
                        totalAdded,
                        totalUpdated,
                        totalFailed,
                        matchResults
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncMatchStatisticsByLeagueAndSeasonAsync for tournament {ApiTournamentId}, season {ApiSeasonId}",
                    apiTournamentId, apiSeasonId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
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
                        existingSeason.LeagueId = league.LeagueId;
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

        public async Task<object> SyncMatchEventsAsync(int apiFixtureId)
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
                        message = $"Match with ApiFixtureId {apiFixtureId} not found. Please sync matches first.",
                        data = (object)null
                    };
                }

                string url = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/incidents";
                _logger.LogInformation("Fetching incidents for event {EventId}", apiFixtureId);

                var json = await FetchJson(url);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("incidents", out var incidents))
                {
                    return new
                    {
                        status = false,
                        message = $"No incidents found for match {apiFixtureId}",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;
                int skipped = 0;

                foreach (var incident in incidents.EnumerateArray())
                {
                            if (!incident.TryGetProperty("id", out var idEl))
                            {
                                skipped++;
                                continue;
                            }

                            int apiEventId = idEl.GetInt32();

                            var existingEvent = await _context.MatchEvents
                                .FirstOrDefaultAsync(e => e.ApiEventId == apiEventId);

                            string incidentType = incident.TryGetProperty("incidentType", out var typeEl)
                                ? typeEl.GetString()
                                : null;

                            if (incidentType == "period")
                            {
                                _logger.LogDebug("Skipping period event {ApiEventId}", apiEventId);
                                continue;
                            }

                            if (incidentType == "injuryTime")
                            {
                                _logger.LogDebug("Skipping injury time event {ApiEventId}", apiEventId);
                                continue;
                            }

                            var matchEvent = new MatchEvent
                            {
                                ApiEventId = apiEventId,
                                MatchId = match.MatchId,
                                EventType = incidentType,
                                Detail = incident.TryGetProperty("incidentClass", out var classEl)
                                    ? classEl.GetString()
                                    : null,
                                EventTime = incident.TryGetProperty("time", out var timeEl)
                                    ? timeEl.GetInt32()
                                    : null,
                                ExtraTime = incident.TryGetProperty("addedTime", out var addedTimeEl)
                                    ? addedTimeEl.GetInt32()
                                    : null,
                                Period = incident.TryGetProperty("reversedPeriodTime", out var periodEl)
                                    ? (periodEl.GetInt32() == 1 ? "1st Half" : "2nd Half")
                                    : "REGULAR",
                                Comments = incident.TryGetProperty("text", out var textEl)
                                    ? textEl.GetString()
                                    : null
                            };

                            if (incident.TryGetProperty("isHome", out var isHomeEl))
                            {
                                bool isHome = isHomeEl.GetBoolean();
                                matchEvent.TeamId = isHome ? match.HomeTeamId : match.AwayTeamId;
                            }

                            if (incidentType == "substitution")
                            {

                                if (incident.TryGetProperty("playerOut", out var playerOutEl))
                                {
                                    if (playerOutEl.TryGetProperty("id", out var playerOutIdEl))
                                    {
                                        int apiPlayerOutId = playerOutIdEl.GetInt32();
                                        var playerOut = await _context.Players
                                            .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayerOutId);

                                        matchEvent.PlayerId = playerOut?.PlayerId;

                                        if (playerOut != null)
                                        {
                                            _logger.LogDebug("Player OUT: {PlayerName} (API ID: {ApiId})",
                                                playerOut.FullName, apiPlayerOutId);
                                        }
                                    }
                                }

                                if (incident.TryGetProperty("playerIn", out var playerInEl))
                                {
                                    if (playerInEl.TryGetProperty("id", out var playerInIdEl))
                                    {
                                        int apiPlayerInId = playerInIdEl.GetInt32();
                                        var playerIn = await _context.Players
                                            .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayerInId);

                                        matchEvent.AssistPlayerId = playerIn?.PlayerId;

                                        if (playerIn != null)
                                        {
                                            _logger.LogDebug("Player IN: {PlayerName} (API ID: {ApiId})",
                                                playerIn.FullName, apiPlayerInId);
                                        }
                                    }
                                }

                                if (matchEvent.PlayerId.HasValue && matchEvent.AssistPlayerId.HasValue)
                                {
                                    matchEvent.Comments = $"Substitution: Player out (ID: {matchEvent.PlayerId}) replaced by Player in (ID: {matchEvent.AssistPlayerId})";
                                }
                            }
                            else if (incidentType == "goal")
                            {
                                if (incident.TryGetProperty("player", out var scorerEl))
                                {
                                    if (scorerEl.TryGetProperty("id", out var scorerIdEl))
                                    {
                                        int apiScorerId = scorerIdEl.GetInt32();
                                        var scorer = await _context.Players
                                            .FirstOrDefaultAsync(p => p.ApiPlayerId == apiScorerId);

                                        matchEvent.PlayerId = scorer?.PlayerId;

                                        if (scorer != null)
                                        {
                                            _logger.LogDebug("Goal scorer: {PlayerName} (API ID: {ApiId})",
                                                scorer.FullName, apiScorerId);
                                        }
                                    }
                                }

                                if (incident.TryGetProperty("assist", out var assistEl))
                                {
                                    if (assistEl.TryGetProperty("id", out var assistIdEl))
                                    {
                                        int apiAssistId = assistIdEl.GetInt32();
                                        var assistPlayer = await _context.Players
                                            .FirstOrDefaultAsync(p => p.ApiPlayerId == apiAssistId);

                                        matchEvent.AssistPlayerId = assistPlayer?.PlayerId;

                                        if (assistPlayer != null)
                                        {
                                            _logger.LogDebug("Goal assist: {PlayerName} (API ID: {ApiId})",
                                                assistPlayer.FullName, apiAssistId);
                                        }
                                    }
                                }

                                if (incident.TryGetProperty("from", out var fromEl))
                                {
                                    string goalType = fromEl.GetString();
                                    matchEvent.Comments = $"Goal from {goalType}";
                                }
                            }
                            else if (incidentType == "card")
                            {
                                if (incident.TryGetProperty("player", out var playerEl))
                                {
                                    if (playerEl.TryGetProperty("id", out var playerIdEl))
                                    {
                                        int apiPlayerId = playerIdEl.GetInt32();
                                        var player = await _context.Players
                                            .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayerId);

                                        matchEvent.PlayerId = player?.PlayerId;

                                        if (player != null)
                                        {
                                            _logger.LogDebug("Card for player: {PlayerName} (API ID: {ApiId})",
                                                player.FullName, apiPlayerId);
                                        }
                                    }
                                }

                                if (incident.TryGetProperty("reason", out var reasonEl))
                                {
                                    string reason = reasonEl.GetString();
                                    matchEvent.Comments = $"Reason: {reason}";
                                }
                            }

                            if (existingEvent == null)
                            {
                                _context.MatchEvents.Add(matchEvent);
                                added++;
                                _logger.LogDebug("Added event {ApiEventId} for match {MatchId}: {EventType} at {Time} minute",
                                    apiEventId, match.MatchId, matchEvent.EventType, matchEvent.EventTime);
                            }
                            else
                            {
                                existingEvent.MatchId = matchEvent.MatchId;
                                existingEvent.TeamId = matchEvent.TeamId;
                                existingEvent.PlayerId = matchEvent.PlayerId;
                                existingEvent.AssistPlayerId = matchEvent.AssistPlayerId;
                                existingEvent.EventType = matchEvent.EventType;
                                existingEvent.Detail = matchEvent.Detail;
                                existingEvent.EventTime = matchEvent.EventTime;
                                existingEvent.ExtraTime = matchEvent.ExtraTime;
                                existingEvent.Period = matchEvent.Period;
                                existingEvent.Comments = matchEvent.Comments ?? existingEvent.Comments;

                                _context.MatchEvents.Update(existingEvent);
                                updated++;
                                _logger.LogDebug("Updated event {ApiEventId} for match {MatchId}",
                                    apiEventId, match.MatchId);
                            }
                        }

                        await _context.SaveChangesAsync();

                        return new
                        {
                            status = true,
                            message = $"Synced events for match {apiFixtureId}: {added} added, {updated} updated, {skipped} skipped",
                            data = new
                            {
                                added,
                                updated,
                                skipped,
                                matchId = match.MatchId,
                                apiFixtureId
                            }
                        };
                    }
                
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing match events for fixture {ApiFixtureId}", apiFixtureId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncStandingsAsync(int apiTournamentId, int apiSeasonId)
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found. Please sync seasons first.",
                        data = (object)null
                    };
                }

                string url = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/standings/total";
                _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);

                var json = await FetchJson(url);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("standings", out var standings))
                {
                    return new
                    {
                        status = false,
                        message = "No standings data found in response",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;
                int skipped = 0;
                var processedTeams = new HashSet<int>();

                foreach (var standingGroup in standings.EnumerateArray())
                {
                    if (!standingGroup.TryGetProperty("rows", out var rows))
                        continue;

                    foreach (var row in rows.EnumerateArray())
                    {
                        if (!row.TryGetProperty("team", out var teamEl))
                        {
                            skipped++;
                            continue;
                        }

                        if (!teamEl.TryGetProperty("id", out var teamIdEl))
                        {
                            skipped++;
                            continue;
                        }

                        int apiTeamId = teamIdEl.GetInt32();

                        var team = await _context.Teams
                            .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);

                        if (team == null)
                        {
                            _logger.LogWarning("Team with API ID {ApiTeamId} not found in database. Skipping standings entry.",
                                apiTeamId);
                            skipped++;
                            continue;
                        }

                        if (processedTeams.Contains(team.TeamId))
                        {
                            _logger.LogDebug("Duplicate standings entry for team {TeamId} in league {LeagueId} season {SeasonId}",
                                team.TeamId, league.LeagueId, season.SeasonId);
                            continue;
                        }
                        processedTeams.Add(team.TeamId);

                        string homeRecord = null;
                        if (row.TryGetProperty("home", out var homeRecordEl))
                        {
                            int homeWins = homeRecordEl.TryGetProperty("wins", out var hw) ? hw.GetInt32() : 0;
                            int homeDraws = homeRecordEl.TryGetProperty("draws", out var hd) ? hd.GetInt32() : 0;
                            int homeLosses = homeRecordEl.TryGetProperty("losses", out var hl) ? hl.GetInt32() : 0;
                            homeRecord = $"{homeWins}-{homeDraws}-{homeLosses}";
                        }

                        string awayRecord = null;
                        if (row.TryGetProperty("away", out var awayRecordEl))
                        {
                            int awayWins = awayRecordEl.TryGetProperty("wins", out var aw) ? aw.GetInt32() : 0;
                            int awayDraws = awayRecordEl.TryGetProperty("draws", out var ad) ? ad.GetInt32() : 0;
                            int awayLosses = awayRecordEl.TryGetProperty("losses", out var al) ? al.GetInt32() : 0;
                            awayRecord = $"{awayWins}-{awayDraws}-{awayLosses}";
                        }

                        var standing = new Standing
                        {
                            LeagueId = league.LeagueId,
                            SeasonId = season.SeasonId,
                            TeamId = team.TeamId,
                            Rank = row.TryGetProperty("rank", out var rank) ? rank.GetInt32() : null,
                            Played = row.TryGetProperty("matches", out var matches) ? matches.GetInt32() : null,
                            Win = row.TryGetProperty("wins", out var wins) ? wins.GetInt32() : null,
                            Draw = row.TryGetProperty("draws", out var draws) ? draws.GetInt32() : null,
                            Loss = row.TryGetProperty("losses", out var losses) ? losses.GetInt32() : null,
                            GoalsFor = row.TryGetProperty("scoresFor", out var scoresFor) ? scoresFor.GetInt32() : null,
                            GoalsAgainst = row.TryGetProperty("scoresAgainst", out var scoresAgainst) ? scoresAgainst.GetInt32() : null,
                            Points = row.TryGetProperty("points", out var points) ? points.GetInt32() : null,
                            Form = row.TryGetProperty("form", out var form) ? form.GetString() : null,
                            Description = row.TryGetProperty("description", out var description) ? description.GetString() : null,
                            HomeRecord = homeRecord,
                            AwayRecord = awayRecord,
                            ApiLastUpdated = DateTime.UtcNow
                        };

                        if (standing.GoalsFor.HasValue && standing.GoalsAgainst.HasValue)
                        {
                            standing.GoalDifference = standing.GoalsFor.Value - standing.GoalsAgainst.Value;
                        }

                        var existingStanding = await _context.Standings
                            .FirstOrDefaultAsync(s => s.LeagueId == league.LeagueId &&
                                                      s.SeasonId == season.SeasonId &&
                                                      s.TeamId == team.TeamId);

                        if (existingStanding == null)
                        {
                            _context.Standings.Add(standing);
                            added++;
                        }
                        else
                        {
                            existingStanding.Rank = standing.Rank;
                            existingStanding.Played = standing.Played;
                            existingStanding.Win = standing.Win;
                            existingStanding.Draw = standing.Draw;
                            existingStanding.Loss = standing.Loss;
                            existingStanding.GoalsFor = standing.GoalsFor;
                            existingStanding.GoalsAgainst = standing.GoalsAgainst;
                            existingStanding.GoalDifference = standing.GoalDifference;
                            existingStanding.Points = standing.Points;
                            existingStanding.Form = standing.Form;
                            existingStanding.Description = standing.Description;
                            existingStanding.HomeRecord = standing.HomeRecord;
                            existingStanding.AwayRecord = standing.AwayRecord;
                            existingStanding.ApiLastUpdated = standing.ApiLastUpdated;

                            _context.Standings.Update(existingStanding);
                            updated++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Synced standings for {league.LeagueName} {season.Year}: {added} added, {updated} updated, {skipped} skipped",
                    data = new
                    {
                        added,
                        updated,
                        skipped,
                        leagueId = league.LeagueId,
                        seasonId = season.SeasonId,
                        leagueName = league.LeagueName,
                        seasonYear = season.Year
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing standings for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> FetchPlayerMatchStatsByApiMatchIdAsync(int apiFixtureId)
        {
            try
            {
                var match = await _context.Matches
                    .Include(m => m.HomeTeam)
                    .Include(m => m.AwayTeam)
                    .FirstOrDefaultAsync(m => m.ApiFixtureId == apiFixtureId);

                if (match == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Match with API ID {apiFixtureId} not found in database. Please sync matches first.",
                        data = (object)null
                    };
                }

                // Get all players from both teams
                var players = await _context.Players
                    .Where(p => p.TeamId == match.HomeTeamId || p.TeamId == match.AwayTeamId)
                    .Where(p => p.ApiPlayerId.HasValue)
                    .ToListAsync();

                if (!players.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No players found for teams in this match. Please sync teams and players first.",
                        data = (object)null
                    };
                }

                int successCount = 0;
                int failCount = 0;
                var results = new List<object>();

                // Fetch shotmap once for the whole match, group by player API ID
                var penaltyScored = new Dictionary<int, int>();
                var penaltyMissed = new Dictionary<int, int>();
                var penaltyWon = new Dictionary<int, int>();
                try
                {
                    var shotmapJson = await FetchJson($"https://www.sofascore.com/api/v1/event/{apiFixtureId}/shotmap");
                    using var shotmapDoc = JsonDocument.Parse(shotmapJson);
                    if (shotmapDoc.RootElement.TryGetProperty("shotmap", out var shots))
                    {
                        foreach (var shot in shots.EnumerateArray())
                        {
                            if (!shot.TryGetProperty("player", out var sp) || !sp.TryGetProperty("id", out var pid)) continue;
                            int playerId = pid.GetInt32();
                            string situation = shot.TryGetProperty("situation", out var sit) ? sit.GetString() ?? "" : "";
                            string shotType = shot.TryGetProperty("shotType", out var st) ? st.GetString() ?? "" : "";

                            if (situation == "penalty")
                            {
                                if (shotType == "goal")
                                    penaltyScored[playerId] = penaltyScored.GetValueOrDefault(playerId) + 1;
                                else
                                    penaltyMissed[playerId] = penaltyMissed.GetValueOrDefault(playerId) + 1;
                            }
                        }
                    }
                }
                catch { /* shotmap optional, continue without it */ }

                foreach (var player in players)
                {
                    try
                    {
                        var statsUrl = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/player/{player.ApiPlayerId}/statistics";
                        string statsJson = await FetchJson(statsUrl);

                        using var doc = JsonDocument.Parse(statsJson);
                        var playerStats = ExtractPlayerMatchStatisticsFromJson(doc.RootElement, match, player);

                        // Apply shotmap penalty data
                        if (playerStats != null && player.ApiPlayerId.HasValue)
                        {
                            int apiId = player.ApiPlayerId.Value;
                            if (penaltyScored.TryGetValue(apiId, out int ps)) playerStats.PenaltiesScored = ps;
                            if (penaltyMissed.TryGetValue(apiId, out int pm)) playerStats.PenaltiesMissed = pm;
                        }

                        if (playerStats != null)
                        {
                            var existingStats = await _context.PlayerMatchStatistics
                                .FirstOrDefaultAsync(ps => ps.MatchId == match.MatchId &&
                                                           ps.PlayerId == player.PlayerId);

                            if (existingStats == null)
                            {
                                _context.PlayerMatchStatistics.Add(playerStats);
                                successCount++;
                            }
                            else
                            {
                                UpdateExistingPlayerMatchStatistics(existingStats, playerStats);
                                _context.PlayerMatchStatistics.Update(existingStats);
                                successCount++;
                            }
                        }
                        else
                        {
                            failCount++;
                        }

                        results.Add(new
                        {
                            playerId = player.PlayerId,
                            playerName = player.FullName,
                            apiPlayerId = player.ApiPlayerId,
                            success = playerStats != null
                        });

                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch stats for player {PlayerId}", player.PlayerId);
                        failCount++;
                        results.Add(new
                        {
                            playerId = player.PlayerId,
                            playerName = player.FullName,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Fetched player match statistics for match {apiFixtureId}: {successCount} successful, {failCount} failed",
                    data = new
                    {
                        matchId = match.MatchId,
                        apiFixtureId,
                        homeTeam = match.HomeTeam?.TeamName,
                        awayTeam = match.AwayTeam?.TeamName,
                        totalPlayers = players.Count,
                        successCount,
                        failCount,
                        results
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching player match statistics for match {ApiFixtureId}", apiFixtureId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }
        public async Task<object> FetchPlayerMatchStatsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found. Please sync seasons first.",
                        data = (object)null
                    };
                }

                // Get all finished matches for this league and season
                var matches = await _context.Matches
                    .Include(m => m.HomeTeam)
                    .Include(m => m.AwayTeam)
                    .Where(m => m.LeagueId == league.LeagueId &&
                               m.SeasonId == season.SeasonId &&
                               m.Status == "finished" &&
                               m.ApiFixtureId.HasValue) // Only get matches with valid API fixture ID
                    .OrderBy(m => m.MatchDate)
                    .ToListAsync();

                if (!matches.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No finished matches found for {league.LeagueName} season {season.Year}",
                        data = (object)null
                    };
                }

                int totalMatchesProcessed = 0;
                int totalSuccess = 0;
                int totalFailed = 0;
                var matchResults = new List<object>();

                foreach (var match in matches)
                {
                    // Convert int? to int by using .Value (safe because we filtered for HasValue)
                    int apiFixtureId = match.ApiFixtureId.Value;

                    // Fetch stats for this match
                    var resultObject = await FetchPlayerMatchStatsByApiMatchIdAsync(apiFixtureId);

                    // Parse the result to get success/failure counts
                    var resultType = resultObject.GetType();
                    var statusProp = resultType.GetProperty("status");
                    var isSuccess = statusProp != null && (bool)statusProp.GetValue(resultObject);

                    if (isSuccess)
                    {
                        var dataProp = resultType.GetProperty("data");
                        if (dataProp != null)
                        {
                            var data = dataProp.GetValue(resultObject);
                            var dataType = data?.GetType();

                            var successCountProp = dataType?.GetProperty("successCount");
                            var failCountProp = dataType?.GetProperty("failCount");

                            if (successCountProp != null)
                                totalSuccess += (int)successCountProp.GetValue(data);
                            if (failCountProp != null)
                                totalFailed += (int)failCountProp.GetValue(data);
                        }
                        totalMatchesProcessed++;
                    }

                    matchResults.Add(new
                    {
                        matchId = match.MatchId,
                        apiFixtureId = apiFixtureId,
                        matchDate = match.MatchDate,
                        homeTeam = match.HomeTeam?.TeamName,
                        awayTeam = match.AwayTeam?.TeamName,
                        success = isSuccess
                    });

                    // Delay to avoid rate limiting
                    await Task.Delay(1000);
                }

                return new
                {
                    status = true,
                    message = $"Fetched player match statistics for {league.LeagueName} {season.Year}: " +
                              $"{totalMatchesProcessed} matches processed, {totalSuccess} player stats added/updated, {totalFailed} failed",
                    data = new
                    {
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        seasonId = season.SeasonId,
                        seasonYear = season.Year,
                        totalMatches = matches.Count,
                        totalMatchesProcessed,
                        totalSuccess,
                        totalFailed,
                        matchResults
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching player match statistics for league {ApiTournamentId}, season {ApiSeasonId}",
                    apiTournamentId, apiSeasonId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }
        public async Task<object> FetchPlayerMatchStatsByRoundAsync(int apiTournamentId, int apiSeasonId, string round)
                {
                    try
                    {
                        var league = await _context.Leagues.FirstOrDefaultAsync(l => l.ApiLeagueId == apiTournamentId);
                        if (league == null)
                            return new { status = false, message = $"League with API ID {apiTournamentId} not found.", data = (object)null };

                        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId);
                        if (season == null)
                            return new { status = false, message = $"Season with API ID {apiSeasonId} not found.", data = (object)null };

                        var matches = await _context.Matches
                            .Include(m => m.HomeTeam)
                            .Include(m => m.AwayTeam)
                            .Where(m => m.LeagueId == league.LeagueId &&
                                        m.SeasonId == season.SeasonId &&
                                        m.Status == "finished" &&
                                        m.ApiFixtureId.HasValue &&
                                        m.Round == round)
                            .OrderBy(m => m.MatchDate)
                            .ToListAsync();

                        if (!matches.Any())
                            return new { status = false, message = $"No finished matches found for round '{round}'", data = (object)null };

                        int totalMatchesProcessed = 0, totalSuccess = 0, totalFailed = 0;
                        var matchResults = new List<object>();

                        foreach (var match in matches)
                        {
                            var resultObject = await FetchPlayerMatchStatsByApiMatchIdAsync(match.ApiFixtureId!.Value);
                            var resultType = resultObject.GetType();
                            var isSuccess = resultType.GetProperty("status") is { } sp && (bool)sp.GetValue(resultObject);

                            if (isSuccess)
                            {
                                var data = resultType.GetProperty("data")?.GetValue(resultObject);
                                var dataType = data?.GetType();
                                if (dataType?.GetProperty("successCount")?.GetValue(data) is int sc) totalSuccess += sc;
                                if (dataType?.GetProperty("failCount")?.GetValue(data) is int fc) totalFailed += fc;
                                totalMatchesProcessed++;
                            }

                            matchResults.Add(new
                            {
                                matchId = match.MatchId,
                                apiFixtureId = match.ApiFixtureId!.Value,
                                matchDate = match.MatchDate,
                                homeTeam = match.HomeTeam?.TeamName,
                                awayTeam = match.AwayTeam?.TeamName,
                                success = isSuccess
                            });

                            await Task.Delay(1000);
                        }

                        return new
                        {
                            status = true,
                            message = $"Round '{round}' — {league.LeagueName} {season.Year}: {totalMatchesProcessed} matches, {totalSuccess} stats saved, {totalFailed} failed",
                            data = new
                            {
                                leagueId = league.LeagueId,
                                leagueName = league.LeagueName,
                                seasonId = season.SeasonId,
                                seasonYear = season.Year,
                                round,
                                totalMatches = matches.Count,
                                totalMatchesProcessed,
                                totalSuccess,
                                totalFailed,
                                matchResults
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching player match stats for round {Round}", round);
                        return new { status = false, message = ex.Message, data = ex.StackTrace };
                    }
                }

        // ==================== GetAll (đọc từ DB) ====================

        public async Task<List<League>> GetAllLeaguesAsync()
        {
            return await _context.Leagues.ToListAsync();
        }

        public async Task<List<SeasonListItemDto>> GetAllSeasonsAsync(int? leagueId = null, int? tournamentId = null)
        {
            League leagueEntity = null;
            int? resolvedLeagueId = leagueId;

            if (tournamentId.HasValue && tournamentId.Value > 0)
            {
                leagueEntity = await _context.Leagues
                    .FirstOrDefaultAsync(l => l.ApiLeagueId == tournamentId.Value);
                if (leagueEntity != null)
                    resolvedLeagueId = leagueEntity.LeagueId;
            }

            // Chưa sync giải nhưng cần xem mùa: chỉ có Api tournament id → lấy trực tiếp từ SofaScore
            if (tournamentId.HasValue && tournamentId.Value > 0 && leagueEntity == null)
                return await FetchSeasonListFromSofaAsync(tournamentId.Value, null);

            var query = _context.Seasons.AsQueryable();
            if (resolvedLeagueId.HasValue)
                query = query.Where(s => s.LeagueId == resolvedLeagueId.Value);

            var dbList = await query
                .OrderByDescending(s => s.Year)
                .ToListAsync();

            if (dbList.Count > 0)
            {
                return dbList.Select(s => new SeasonListItemDto
                {
                    SeasonId = s.SeasonId,
                    LeagueId = s.LeagueId,
                    Year = s.Year,
                    ApiSeasonId = s.ApiSeasonId
                }).ToList();
            }

            // Đã có giải trong DB nhưng chưa sync mùa → lấy từ API
            if (tournamentId.HasValue && tournamentId.Value > 0)
                return await FetchSeasonListFromSofaAsync(tournamentId.Value, leagueEntity?.LeagueId);

            return new List<SeasonListItemDto>();
        }

        private async Task<List<SeasonListItemDto>> FetchSeasonListFromSofaAsync(int apiTournamentId, int? leagueIdHint)
        {
            var seasonsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/seasons";
            string seasonsJson;
            try
            {
                seasonsJson = await FetchJson(seasonsUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FetchSeasonListFromSofaAsync failed for tournament {TournamentId}", apiTournamentId);
                return new List<SeasonListItemDto>();
            }

            using var doc = JsonDocument.Parse(seasonsJson);
            if (!doc.RootElement.TryGetProperty("seasons", out var seasons))
                return new List<SeasonListItemDto>();

            var parsed = new List<(int apiId, int? year)>();
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

                parsed.Add((apiSeasonId, year));
            }

            if (parsed.Count == 0)
                return new List<SeasonListItemDto>();

            var apiIds = parsed.Select(p => p.apiId).ToList();
            var dbSeasons = await _context.Seasons
                .Where(s => s.ApiSeasonId.HasValue && apiIds.Contains(s.ApiSeasonId.Value))
                .ToListAsync();
            var byApiId = dbSeasons
                .Where(s => s.ApiSeasonId.HasValue)
                .ToDictionary(s => s.ApiSeasonId!.Value, s => s);

            var list = new List<SeasonListItemDto>();
            foreach (var p in parsed)
            {
                byApiId.TryGetValue(p.apiId, out var db);
                list.Add(new SeasonListItemDto
                {
                    SeasonId = db?.SeasonId,
                    LeagueId = db?.LeagueId ?? leagueIdHint,
                    Year = db?.Year ?? p.year,
                    ApiSeasonId = p.apiId
                });
            }

            return list;
        }

        public async Task<List<Team>> GetAllTeamsAsync()
        {
            return await _context.Teams.ToListAsync();
        }

        public async Task<List<Team>> GetTeamsByIdsAsync(List<int> teamIds)
        {
            if (teamIds == null || teamIds.Count == 0) return new List<Team>();
            return await _context.Teams.Where(t => teamIds.Contains(t.TeamId)).ToListAsync();
        }

        public async Task<List<Match>> GetTeamLastMatchesFromDbAsync(int apiTeamId, int count = 5)
        {
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);
            if (team == null) return new List<Match>();
            return await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => (m.HomeTeamId == team.TeamId || m.AwayTeamId == team.TeamId) && m.Status == "finished")
                .OrderByDescending(m => m.MatchDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Match>> GetAllMatchesAsync(int? tournamentId = null, int? seasonId = null)
        {
            var query = _context.Matches.AsQueryable();
            if (tournamentId.HasValue)
            {
                var league = await _context.Leagues.FirstOrDefaultAsync(l => l.ApiLeagueId == tournamentId.Value);
                if (league != null) query = query.Where(m => m.LeagueId == league.LeagueId);
            }
            if (seasonId.HasValue)
            {
                var season = await _context.Seasons.FirstOrDefaultAsync(s => s.ApiSeasonId == seasonId.Value);
                if (season != null) query = query.Where(m => m.SeasonId == season.SeasonId);
            }
            return await query.ToListAsync();
        }

        public async Task<List<MatchStatistic>> GetAllMatchStatisticsAsync()
        {
            return await _context.MatchStatistics.ToListAsync();
        }

        public async Task<List<Player>> GetAllPlayersAsync(int? teamId = null, int? sofascoreTeamId = null)
        {
            int? resolvedTeamId = teamId;

            if (sofascoreTeamId.HasValue && sofascoreTeamId.Value > 0)
            {
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.ApiTeamId == sofascoreTeamId.Value);
                if (team == null)
                    return new List<Player>();
                resolvedTeamId = team.TeamId;
            }

            var query = _context.Players.AsQueryable();
            if (resolvedTeamId.HasValue)
                query = query.Where(p => p.TeamId == resolvedTeamId.Value);
            return await query.ToListAsync();
        }

        public async Task<List<Player>> GetAllTeamPlayersByLeagueSeasonAsync(int tournamentId, int seasonId)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == tournamentId);

            if (league == null)
                return new List<Player>();

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.ApiSeasonId == seasonId && s.LeagueId == league.LeagueId);

            if (season == null)
                return new List<Player>();

            var teamIds = await _context.Standings
                .Where(s => s.LeagueId == league.LeagueId && s.SeasonId == season.SeasonId && s.TeamId.HasValue)
                .Select(s => s.TeamId!.Value)
                .Distinct()
                .ToListAsync();

            if (teamIds.Count == 0)
            {
                var matchRows = await _context.Matches
                    .AsNoTracking()
                    .Where(m => m.LeagueId == league.LeagueId && m.SeasonId == season.SeasonId)
                    .Select(m => new { m.HomeTeamId, m.AwayTeamId })
                    .ToListAsync();

                teamIds = matchRows
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();
            }

            if (teamIds.Count == 0)
            {
                teamIds = await _context.Teams
                    .Where(t => t.LeagueId == league.LeagueId)
                    .Select(t => t.TeamId)
                    .ToListAsync();
            }

            if (teamIds.Count == 0)
                return new List<Player>();

            return await _context.Players
                .Where(p => p.TeamId.HasValue && teamIds.Contains(p.TeamId.Value))
                .ToListAsync();
        }

        public async Task<List<PlayerSeasonStatistic>> GetAllPlayerSeasonStatisticsAsync()
        {
            return await _context.PlayerSeasonStatistics.ToListAsync();
        }

        public async Task<object> AggregateSeasonStatsFromMatchStatsAsync(int? leagueId = null, int? seasonId = null, int? playerId = null)
        {
            try
            {
                // Get all matches for the given league/season
                var matchQuery = _context.Matches
                    .Include(m => m.Season)
                    .AsQueryable();

                if (leagueId.HasValue)
                    matchQuery = matchQuery.Where(m => m.Season != null && m.Season.LeagueId == leagueId.Value);
                if (seasonId.HasValue)
                    matchQuery = matchQuery.Where(m => m.Season != null && m.Season.SeasonId == seasonId.Value);

                var matchIds = await matchQuery.Select(m => new { m.MatchId, m.Season.SeasonId, m.Season.LeagueId }).ToListAsync();
                if (!matchIds.Any())
                    return new { status = false, message = "No matches found for given filters" };

                // Get all player match stats for these matches
                var matchIdSet = matchIds.Select(m => m.MatchId).ToHashSet();
                var matchStats = await _context.PlayerMatchStatistics
                    .Where(s => s.MatchId.HasValue && matchIdSet.Contains(s.MatchId.Value))
                    .Where(s => !playerId.HasValue || s.PlayerId == playerId.Value)
                    .ToListAsync();

                // Build a lookup: matchId → (seasonId, leagueId)
                var matchSeasonMap = matchIds.ToDictionary(m => m.MatchId, m => (m.SeasonId, m.LeagueId));

                // Group by (playerId, teamId, seasonId, leagueId) and aggregate
                var groups = matchStats
                    .Where(s => s.PlayerId.HasValue && s.MatchId.HasValue && matchSeasonMap.ContainsKey(s.MatchId.Value))
                    .GroupBy(s => (
                        PlayerId: s.PlayerId!.Value,
                        TeamId: s.TeamId,
                        SeasonId: matchSeasonMap[s.MatchId!.Value].SeasonId,
                        LeagueId: matchSeasonMap[s.MatchId!.Value].LeagueId
                    ));

                int updated = 0;
                foreach (var g in groups)
                {
                    var existing = await _context.PlayerSeasonStatistics
                        .FirstOrDefaultAsync(x =>
                            x.PlayerId == g.Key.PlayerId &&
                            x.SeasonId == g.Key.SeasonId &&
                            x.LeagueId == g.Key.LeagueId);

                    if (existing == null) continue; // Only update existing rows, don't create new ones

                    // Aggregate all stats from match stats that Sofascore season API doesn't provide
                    existing.Tackles            = g.Sum(s => s.Tackles ?? 0);
                    existing.Interceptions      = g.Sum(s => s.Interceptions ?? 0);
                    existing.DuelsWon           = g.Sum(s => s.DuelsWon ?? 0);
                    existing.DuelsTotal         = g.Sum(s => s.DuelsTotal ?? 0);
                    existing.DuelsWonRate       = existing.DuelsTotal > 0
                        ? Math.Round((decimal)existing.DuelsWon.Value / existing.DuelsTotal.Value * 100, 2)
                        : null;
                    existing.FoulsDrawn         = g.Sum(s => s.FoulsDrawn ?? 0);
                    existing.DribblesAttempted  = g.Sum(s => s.DribblesAttempted ?? 0);
                    var dribblesSuccess         = g.Sum(s => s.DribblesSuccess ?? 0);
                    existing.DribblesSuccessRate = existing.DribblesAttempted > 0
                        ? Math.Round((decimal)dribblesSuccess / existing.DribblesAttempted.Value * 100, 2)
                        : null;
                    existing.PenaltiesMissed    = g.Sum(s => s.PenaltiesMissed ?? 0);
                    // GK stats from match stats
                    existing.SavesInsideBox     = g.Sum(s => s.SavesInsideBox ?? 0) > 0 ? g.Sum(s => s.SavesInsideBox ?? 0) : existing.SavesInsideBox;
                    existing.Punches            = g.Sum(s => s.Punches ?? 0) > 0 ? g.Sum(s => s.Punches ?? 0) : existing.Punches;
                    existing.RunsOut            = g.Sum(s => s.RunsOut ?? 0) > 0 ? g.Sum(s => s.RunsOut ?? 0) : existing.RunsOut;
                    existing.RunsOutSuccessful  = g.Sum(s => s.RunsOutSuccessful ?? 0) > 0 ? g.Sum(s => s.RunsOutSuccessful ?? 0) : existing.RunsOutSuccessful;
                    existing.HighClaims         = g.Sum(s => s.HighClaims ?? 0) > 0 ? g.Sum(s => s.HighClaims ?? 0) : existing.HighClaims;
                    existing.PenaltiesSaved     = g.Sum(s => s.PenaltiesSaved ?? 0) > 0 ? g.Sum(s => s.PenaltiesSaved ?? 0) : existing.PenaltiesSaved;

                    _context.PlayerSeasonStatistics.Update(existing);
                    updated++;
                }

                await _context.SaveChangesAsync();
                return new { status = true, message = $"Aggregated match stats into {updated} season stat rows" };
            }
            catch (Exception ex)
            {
                return new { status = false, message = ex.Message };
            }
        }

        public async Task<object> SyncPlayerStatsByPlayerIdAsync(int playerId)
        {
            try
            {
                var player = await _context.Players.FirstOrDefaultAsync(p => p.PlayerId == playerId);
                if (player == null)
                    return new { status = false, message = $"Player {playerId} not found in DB" };
                if (!player.ApiPlayerId.HasValue)
                    return new { status = false, message = $"Player {playerId} has no ApiPlayerId" };

                // Get all league-season combos from DB
                var leagueSeasons = await _context.Seasons
                    .Include(s => s.League)
                    .Where(s => s.League != null && s.League.ApiLeagueId > 0 && s.ApiSeasonId > 0)
                    .Select(s => new { 
                        ApiLeagueId = s.League.ApiLeagueId ?? 0, 
                        ApiSeasonId = s.ApiSeasonId ?? 0, 
                        s.SeasonId, s.LeagueId, s.Year 
                    })
                    .Where(s => s.ApiLeagueId > 0 && s.ApiSeasonId > 0)
                    .ToListAsync();

                int added = 0, updated = 0, skipped = 0;
                var details = new List<object>();

                foreach (var ls in leagueSeasons)
                {
                    try
                    {
                        var url = $"https://www.sofascore.com/api/v1/player/{player.ApiPlayerId}/unique-tournament/{ls.ApiLeagueId}/season/{ls.ApiSeasonId}/statistics/overall";
                        var json = await FetchJson(url);
                        using var doc = JsonDocument.Parse(json);
                        if (!doc.RootElement.TryGetProperty("statistics", out var s)) { skipped++; continue; }

                        var stat = new PlayerSeasonStatistic
                        {
                            PlayerId = player.PlayerId,
                            TeamId = player.TeamId,
                            LeagueId = ls.LeagueId,
                            SeasonId = ls.SeasonId,
                            Appearances   = s.TryGetProperty("appearances", out var ap) ? ap.GetInt32() : null,
                            Lineups       = s.TryGetProperty("matchesStarted", out var ms) ? ms.GetInt32() : null,
                            Minutes       = s.TryGetProperty("minutesPlayed", out var mp) ? mp.GetInt32() : null,
                            Goals         = s.TryGetProperty("goals", out var g) ? g.GetInt32() : null,
                            Assists       = s.TryGetProperty("assists", out var a) ? a.GetInt32() : null,
                            YellowCards   = s.TryGetProperty("yellowCards", out var yc) ? yc.GetInt32() : null,
                            RedCards      = s.TryGetProperty("redCards", out var rc) ? rc.GetInt32() : null,
                            Rating        = s.TryGetProperty("rating", out var r) ? (decimal?)r.GetDecimal() : null,
                            SubstitutionsIn  = s.TryGetProperty("substitutionsIn", out var si) ? si.GetInt32() : null,
                            SubstitutionsOut = s.TryGetProperty("substitutionsOut", out var so) ? so.GetInt32() : null,
                            ShotsTotal    = s.TryGetProperty("totalShots", out var ts) ? ts.GetInt32() : null,
                            ShotsOnTarget = s.TryGetProperty("shotsOnTarget", out var sot) ? sot.GetInt32() : null,
                            PassesTotal   = s.TryGetProperty("totalPasses", out var pt) ? pt.GetInt32() : null,
                            PassesKey     = s.TryGetProperty("keyPasses", out var kp) ? kp.GetInt32() : null,
                            PassesAccuracy = s.TryGetProperty("accuratePasses", out var pac) ? (decimal?)pac.GetDecimal() : null,
                            DribblesAttempted = s.TryGetProperty("totalDribbleAttempts", out var da) ? da.GetInt32() : null,
                            DribblesSuccess   = s.TryGetProperty("successfulDribbles", out var ds) ? ds.GetInt32() : null,
                            Tackles       = s.TryGetProperty("tackles", out var tk) ? tk.GetInt32() : null,
                            Interceptions = s.TryGetProperty("interceptions", out var ic) ? ic.GetInt32() : null,
                            FoulsCommitted = s.TryGetProperty("fouls", out var fc) ? fc.GetInt32() : null,
                            PenaltiesScored = s.TryGetProperty("penaltyGoals", out var pg) ? pg.GetInt32() : null,
                            // GK
                            Saves         = s.TryGetProperty("saves", out var sv) ? sv.GetInt32() : null,
                            GoalsConceded = s.TryGetProperty("goalsConceded", out var gc2) ? gc2.GetInt32() : null,
                            CleanSheets   = s.TryGetProperty("cleanSheet", out var cs) ? cs.GetInt32() : null,
                            PenaltiesSaved = s.TryGetProperty("penaltySave", out var ps) ? ps.GetInt32() : null,
                        };

                        // Skip if no meaningful data
                        if (stat.Appearances == null && stat.Minutes == null && stat.Rating == null)
                        { skipped++; continue; }

                        var existing = await _context.PlayerSeasonStatistics
                            .FirstOrDefaultAsync(x => x.PlayerId == player.PlayerId && x.SeasonId == ls.SeasonId && x.LeagueId == ls.LeagueId);

                        if (existing == null) { _context.PlayerSeasonStatistics.Add(stat); added++; }
                        else
                        {
                            existing.TeamId = stat.TeamId; existing.Appearances = stat.Appearances;
                            existing.Lineups = stat.Lineups; existing.Minutes = stat.Minutes;
                            existing.Goals = stat.Goals; existing.Assists = stat.Assists;
                            existing.YellowCards = stat.YellowCards; existing.RedCards = stat.RedCards;
                            existing.Rating = stat.Rating; existing.SubstitutionsIn = stat.SubstitutionsIn;
                            existing.SubstitutionsOut = stat.SubstitutionsOut; existing.ShotsTotal = stat.ShotsTotal;
                            existing.ShotsOnTarget = stat.ShotsOnTarget; existing.PassesTotal = stat.PassesTotal;
                            existing.PassesKey = stat.PassesKey; existing.PassesAccuracy = stat.PassesAccuracy;
                            existing.DribblesAttempted = stat.DribblesAttempted; existing.DribblesSuccess = stat.DribblesSuccess;
                            existing.Tackles = stat.Tackles; existing.Interceptions = stat.Interceptions;
                            existing.FoulsCommitted = stat.FoulsCommitted; existing.PenaltiesScored = stat.PenaltiesScored;
                            existing.Saves = stat.Saves; existing.GoalsConceded = stat.GoalsConceded;
                            existing.CleanSheets = stat.CleanSheets; existing.PenaltiesSaved = stat.PenaltiesSaved;
                            updated++;
                        }
                        await _context.SaveChangesAsync();
                        details.Add(new { leagueId = ls.LeagueId, seasonId = ls.SeasonId, year = ls.Year, status = "ok" });
                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        details.Add(new { leagueId = ls.LeagueId, seasonId = ls.SeasonId, year = ls.Year, status = "skipped", reason = ex.Message });
                    }
                }

                // Aggregate defensive stats from match stats for this player
                await AggregateSeasonStatsFromMatchStatsAsync(playerId: player.PlayerId);

                return new
                {
                    status = true,
                    message = $"Synced stats for player {player.FullName} — {added} added, {updated} updated, {skipped} skipped",
                    data = new { added, updated, skipped, details }
                };
            }
            catch (Exception ex)
            {
                return new { status = false, message = ex.Message };
            }
        }

        public async Task<List<MatchEvent>> GetAllMatchEventsAsync()
        {
            return await _context.MatchEvents.ToListAsync();
        }

        public async Task<List<Standing>> GetAllStandingsAsync(int tournamentId, int seasonId)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == tournamentId);

            if (league == null)
                return new List<Standing>();

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.ApiSeasonId == seasonId && s.LeagueId == league.LeagueId);

            if (season == null)
                return new List<Standing>();

            return await _context.Standings
                .Include(s => s.Team)
                .Where(s => s.LeagueId == league.LeagueId && s.SeasonId == season.SeasonId)
                .ToListAsync();
        }

        public Task<bool> MatchExistsByApiFixtureIdAsync(int apiFixtureId)
        {
            return _context.Matches.AnyAsync(m => m.ApiFixtureId == apiFixtureId);
        }

        public async Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByApiFixtureIdAsync(int apiFixtureId, bool fetchIfEmpty = false)
        {
            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.ApiFixtureId == apiFixtureId);

            if (match == null)
                return new List<PlayerMatchStatistic>();

            var list = await _context.PlayerMatchStatistics
                .Include(p => p.Player)
                .Where(p => p.MatchId == match.MatchId)
                .ToListAsync();

            if (list.Count == 0 && fetchIfEmpty)
            {
                var fetchResult = await FetchPlayerMatchStatsByApiMatchIdAsync(apiFixtureId);
                var ok = fetchResult.GetType().GetProperty("status")?.GetValue(fetchResult) is bool st && st;
                if (ok)
                {
                    list = await _context.PlayerMatchStatistics
                        .Include(p => p.Player)
                        .Where(p => p.MatchId == match.MatchId)
                        .ToListAsync();
                }
            }

            return list;
        }

        public async Task<List<PlayerMatchStatistic>> GetAllPlayerMatchStatisticsByLeagueSeasonAsync(int tournamentId, int seasonId)
        {
            var league = await _context.Leagues
                .FirstOrDefaultAsync(l => l.ApiLeagueId == tournamentId);

            if (league == null)
                return new List<PlayerMatchStatistic>();

            var season = await _context.Seasons
                .FirstOrDefaultAsync(s => s.ApiSeasonId == seasonId && s.LeagueId == league.LeagueId);

            if (season == null)
                return new List<PlayerMatchStatistic>();

            var matchIds = await _context.Matches
                .Where(m => m.LeagueId == league.LeagueId && m.SeasonId == season.SeasonId)
                .Select(m => m.MatchId)
                .ToListAsync();

            if (matchIds.Count == 0)
                return new List<PlayerMatchStatistic>();

            return await _context.PlayerMatchStatistics
                .Where(p => p.MatchId.HasValue && matchIds.Contains(p.MatchId.Value))
                .ToListAsync();
        }

        private PlayerMatchStatistic ExtractPlayerMatchStatisticsFromJson(JsonElement root, Match match, Player player)
        {
            var playerStats = new PlayerMatchStatistic
            {
                MatchId = match.MatchId,
                PlayerId = player.PlayerId,
                TeamId = player.TeamId
            };

            if (!root.TryGetProperty("statistics", out var statistics))
            {
                return null;
            }

            if (statistics.TryGetProperty("minutesPlayed", out var minutes))
                playerStats.Minutes = minutes.GetInt32();

            if (statistics.TryGetProperty("goals", out var goals))
                playerStats.Goals = goals.GetInt32();

            if (statistics.TryGetProperty("goalAssist", out var assists))
                playerStats.Assists = assists.GetInt32();

            if (statistics.TryGetProperty("rating", out var rating))
                playerStats.Rating = (decimal)rating.GetDouble();

            if (statistics.TryGetProperty("totalShots", out var totalShots))
                playerStats.Shots = totalShots.GetInt32();

            if (statistics.TryGetProperty("onTargetScoringAttempt", out var shotsOnTarget))
                playerStats.ShotsOnTarget = shotsOnTarget.GetInt32();

            if (statistics.TryGetProperty("expectedGoals", out var expectedGoals))
                playerStats.ExpectedGoals = (decimal)expectedGoals.GetDouble();

            if (statistics.TryGetProperty("expectedAssists", out var expectedAssists))
                playerStats.ExpectedAssists = (decimal)expectedAssists.GetDouble();

            if (statistics.TryGetProperty("totalPass", out var totalPasses))
                playerStats.Passes = totalPasses.GetInt32();

            if (statistics.TryGetProperty("accuratePass", out var accuratePasses))
            {
                if (playerStats.Passes.HasValue && playerStats.Passes.Value > 0)
                {
                    playerStats.PassesAccuracy = (int)Math.Round((double)accuratePasses.GetInt32() / playerStats.Passes.Value * 100);
                }
            }

            if (statistics.TryGetProperty("keyPass", out var keyPasses))
                playerStats.PassesKey = keyPasses.GetInt32();

            if (statistics.TryGetProperty("totalCross", out var totalCrosses))
                playerStats.TotalCrosses = totalCrosses.GetInt32();

            if (statistics.TryGetProperty("accurateCross", out var accurateCrosses))
                playerStats.AccurateCrosses = accurateCrosses.GetInt32();

            if (statistics.TryGetProperty("totalLongBalls", out var totalLongBalls))
                playerStats.TotalLongBalls = totalLongBalls.GetInt32();

            if (statistics.TryGetProperty("accurateLongBalls", out var accurateLongBalls))
                playerStats.AccurateLongBalls = accurateLongBalls.GetInt32();

            if (statistics.TryGetProperty("totalOwnHalfPasses", out var totalOwnHalfPasses))
                playerStats.PassesOwnHalf = totalOwnHalfPasses.GetInt32();

            if (statistics.TryGetProperty("accurateOwnHalfPasses", out var accurateOwnHalfPasses))
                playerStats.AccuratePassesOwnHalf = accurateOwnHalfPasses.GetInt32();

            if (statistics.TryGetProperty("totalOppositionHalfPasses", out var totalOppositionHalfPasses))
                playerStats.PassesOppositionHalf = totalOppositionHalfPasses.GetInt32();

            if (statistics.TryGetProperty("accurateOppositionHalfPasses", out var accurateOppositionHalfPasses))
                playerStats.AccuratePassesOppositionHalf = accurateOppositionHalfPasses.GetInt32();

            if (statistics.TryGetProperty("totalContest", out var totalDribbles))
                playerStats.DribblesAttempted = totalDribbles.GetInt32();

            if (statistics.TryGetProperty("wonContest", out var successfulDribbles))
                playerStats.DribblesSuccess = successfulDribbles.GetInt32();

            if (statistics.TryGetProperty("duelWon", out var duelsWon))
                playerStats.DuelsWon = duelsWon.GetInt32();

            if (statistics.TryGetProperty("duelLost", out var duelsLost))
                playerStats.DuelsTotal = (playerStats.DuelsWon ?? 0) + duelsLost.GetInt32();

            if (statistics.TryGetProperty("aerialWon", out var aerialWon))
                playerStats.AerialDuelsWon = aerialWon.GetInt32();

            if (statistics.TryGetProperty("aerialLost", out var aerialLost))
                playerStats.AerialDuelsLost = aerialLost.GetInt32();

            // GroundDuels = total - aerial
            playerStats.GroundDuelsWon = (playerStats.DuelsWon ?? 0) - (playerStats.AerialDuelsWon ?? 0);
            playerStats.GroundDuelsLost = ((playerStats.DuelsTotal ?? 0) - (playerStats.DuelsWon ?? 0)) - (playerStats.AerialDuelsLost ?? 0);

            if (statistics.TryGetProperty("groundDuelWon", out var groundDuelWon))
                playerStats.GroundDuelsWon = groundDuelWon.GetInt32();

            if (statistics.TryGetProperty("groundDuelLost", out var groundDuelLost))
                playerStats.GroundDuelsLost = groundDuelLost.GetInt32();

            if (statistics.TryGetProperty("totalTackle", out var tackles))
                playerStats.Tackles = tackles.GetInt32();

            if (statistics.TryGetProperty("wonTackle", out var tacklesWon))
                playerStats.TacklesWon = tacklesWon.GetInt32();

            if (statistics.TryGetProperty("interceptionWon", out var interceptions))
                playerStats.Interceptions = interceptions.GetInt32();

            if (statistics.TryGetProperty("ballRecovery", out var ballRecovery))
                playerStats.BallRecoveries = ballRecovery.GetInt32();

            if (statistics.TryGetProperty("totalClearance", out var clearances))
                playerStats.Clearances = clearances.GetInt32();

            if (statistics.TryGetProperty("outfielderBlock", out var blocks))
                playerStats.Blocks = blocks.GetInt32();

            if (statistics.TryGetProperty("fouls", out var fouls))
                playerStats.FoulsCommitted = fouls.GetInt32();

            if (statistics.TryGetProperty("wasFouled", out var wasFouled))
            {
                playerStats.FoulsDrawn = wasFouled.GetInt32();
                playerStats.WasFouled = wasFouled.GetInt32();
            }

            if (statistics.TryGetProperty("yellowCard", out var yellowCard))
                playerStats.YellowCards = yellowCard.GetInt32();

            if (statistics.TryGetProperty("redCard", out var redCard))
                playerStats.RedCards = redCard.GetInt32();

            if (statistics.TryGetProperty("penaltyWon", out var penaltyWon))
                playerStats.PenaltiesWon = penaltyWon.GetInt32();

            if (statistics.TryGetProperty("penaltyConceded", out var penaltyConceded))
                playerStats.PenaltiesCommitted = penaltyConceded.GetInt32();

            if (statistics.TryGetProperty("penaltyMiss", out var penaltyMiss))
                playerStats.PenaltiesMissed = penaltyMiss.GetInt32();

            if (statistics.TryGetProperty("totalOffside", out var offsides))
                playerStats.Offsides = offsides.GetInt32();

            if (statistics.TryGetProperty("touches", out var touches))
                playerStats.Touches = touches.GetInt32();

            if (statistics.TryGetProperty("possessionLostCtrl", out var possessionLost))
                playerStats.PossessionLost = possessionLost.GetInt32();

            if (statistics.TryGetProperty("dispossessed", out var dispossessed))
                playerStats.Dispossessed = dispossessed.GetInt32();

            if (statistics.TryGetProperty("unsuccessfulTouch", out var unsuccessfulTouch))
                playerStats.UnsuccessfulTouch = unsuccessfulTouch.GetInt32();

            // GK stats
            if (statistics.TryGetProperty("saves", out var saves))
                playerStats.Saves = saves.GetInt32();

            if (statistics.TryGetProperty("savedShotsFromInsideTheBox", out var savesInsideBox))
                playerStats.SavesInsideBox = savesInsideBox.GetInt32();

            if (statistics.TryGetProperty("punches", out var punches))
                playerStats.Punches = punches.GetInt32();

            if (statistics.TryGetProperty("totalKeeperSweeper", out var runsOut))
                playerStats.RunsOut = runsOut.GetInt32();

            if (statistics.TryGetProperty("keeperSweeper", out var runsOutSuccessful))
                playerStats.RunsOutSuccessful = runsOutSuccessful.GetInt32();

            if (statistics.TryGetProperty("goodHighClaim", out var highClaims))
                playerStats.HighClaims = highClaims.GetInt32();

            if (statistics.TryGetProperty("goalsConceded", out var goalsConceded))
                playerStats.GoalsConceded = goalsConceded.GetInt32();

            if (statistics.TryGetProperty("penaltySave", out var penaltySave))
                playerStats.PenaltiesSaved = penaltySave.GetInt32();

            if (playerStats.Minutes == null && playerStats.Goals == null &&
                playerStats.Assists == null && playerStats.Rating == null)
            {
                return null;
            }

            return playerStats;
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
            if (newStats.ExpectedAssists.HasValue) existing.ExpectedAssists = newStats.ExpectedAssists;
            if (newStats.GroundDuelsWon.HasValue) existing.GroundDuelsWon = newStats.GroundDuelsWon;
            if (newStats.GroundDuelsLost.HasValue) existing.GroundDuelsLost = newStats.GroundDuelsLost;
            if (newStats.AerialDuelsWon.HasValue) existing.AerialDuelsWon = newStats.AerialDuelsWon;
            if (newStats.AerialDuelsLost.HasValue) existing.AerialDuelsLost = newStats.AerialDuelsLost;
            if (newStats.WasFouled.HasValue) existing.WasFouled = newStats.WasFouled;
            if (newStats.Touches.HasValue) existing.Touches = newStats.Touches;
            if (newStats.PossessionLost.HasValue) existing.PossessionLost = newStats.PossessionLost;
            if (newStats.Dispossessed.HasValue) existing.Dispossessed = newStats.Dispossessed;
            if (newStats.BallRecoveries.HasValue) existing.BallRecoveries = newStats.BallRecoveries;
            if (newStats.TotalCrosses.HasValue) existing.TotalCrosses = newStats.TotalCrosses;
            if (newStats.AccurateCrosses.HasValue) existing.AccurateCrosses = newStats.AccurateCrosses;
            if (newStats.TotalLongBalls.HasValue) existing.TotalLongBalls = newStats.TotalLongBalls;
            if (newStats.AccurateLongBalls.HasValue) existing.AccurateLongBalls = newStats.AccurateLongBalls;
            // GK stats
            if (newStats.Saves.HasValue) existing.Saves = newStats.Saves;
            if (newStats.SavesInsideBox.HasValue) existing.SavesInsideBox = newStats.SavesInsideBox;
            if (newStats.Punches.HasValue) existing.Punches = newStats.Punches;
            if (newStats.RunsOut.HasValue) existing.RunsOut = newStats.RunsOut;
            if (newStats.RunsOutSuccessful.HasValue) existing.RunsOutSuccessful = newStats.RunsOutSuccessful;
            if (newStats.HighClaims.HasValue) existing.HighClaims = newStats.HighClaims;
            if (newStats.GoalsConceded.HasValue) existing.GoalsConceded = newStats.GoalsConceded;
            if (newStats.PenaltiesSaved.HasValue) existing.PenaltiesSaved = newStats.PenaltiesSaved;
        }

        public async Task<object> SyncMatchLineupsAsync(int apiFixtureId)
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
                        message = $"Match with ApiFixtureId {apiFixtureId} not found. Please sync matches first.",
                        data = (object)null
                    };
                }

                string url = $"https://www.sofascore.com/api/v1/event/{apiFixtureId}/lineups";
                _logger.LogInformation("Fetching lineups for event {EventId}", apiFixtureId);

                var json = await FetchJson(url);

                using var doc = JsonDocument.Parse(json);

                int homeAdded = 0;
                int homeUpdated = 0;
                if (doc.RootElement.TryGetProperty("home", out var homeLineup))
                {
                    var result = await ProcessTeamLineup(homeLineup, match.MatchId, match.HomeTeamId);
                    homeAdded = result.added;
                    homeUpdated = result.updated;
                }

                int awayAdded = 0;
                int awayUpdated = 0;
                if (doc.RootElement.TryGetProperty("away", out var awayLineup))
                {
                    var result = await ProcessTeamLineup(awayLineup, match.MatchId, match.AwayTeamId);
                    awayAdded = result.added;
                    awayUpdated = result.updated;
                }

                await _context.SaveChangesAsync();

                int totalAdded = homeAdded + awayAdded;
                int totalUpdated = homeUpdated + awayUpdated;

                return new
                {
                    status = true,
                    message = $"Synced lineups for match {apiFixtureId}: {totalAdded} added, {totalUpdated} updated",
                    data = new
                    {
                        added = totalAdded,
                        updated = totalUpdated,
                        matchId = match.MatchId,
                        apiFixtureId,
                        home = new { added = homeAdded, updated = homeUpdated },
                        away = new { added = awayAdded, updated = awayUpdated }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing match lineups for fixture {ApiFixtureId}", apiFixtureId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        private async Task<(int added, int updated)> ProcessTeamLineup(JsonElement teamLineup, int matchId, int? teamId)
        {
            int added = 0;
            int updated = 0;

            if (teamId == null)
            {
                _logger.LogWarning("Team ID is null for match {MatchId}", matchId);
                return (0, 0);
            }

            string formation = null;
            if (teamLineup.TryGetProperty("formation", out var formationEl))
            {
                formation = formationEl.GetString();
            }

            var existingLineup = await _context.Lineups
                .FirstOrDefaultAsync(l => l.MatchId == matchId && l.TeamId == teamId);

            if (existingLineup == null)
            {
                var newLineup = new Lineup
                {
                    MatchId = matchId,
                    TeamId = teamId,
                    Formation = formation,
                };

                _context.Lineups.Add(newLineup);
                added = 1;
                _logger.LogDebug("Added lineup for team {TeamId} in match {MatchId}", teamId, matchId);
            }
            else
            {
                existingLineup.Formation = formation;

                _context.Lineups.Update(existingLineup);
                updated = 1;
                _logger.LogDebug("Updated lineup for team {TeamId} in match {MatchId}", teamId, matchId);
            }

            return (added, updated);
        }

        public async Task<object> FetchLineupsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
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
                        message = $"League with API ID {apiTournamentId} not found.",
                        data = (object)null
                    };
                }

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found.",
                        data = (object)null
                    };
                }

                var matches = await _context.Matches
                    .Where(m => m.LeagueId == league.LeagueId &&
                               m.SeasonId == season.SeasonId &&
                               m.ApiFixtureId.HasValue &&
                               m.ApiFixtureId > 0)
                    .Select(m => new { m.MatchId, m.ApiFixtureId })
                    .ToListAsync();

                if (!matches.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No matches found for {league.LeagueName} season {season.Year}",
                        data = (object)null
                    };
                }

                int totalAdded = 0;
                int totalUpdated = 0;
                int totalFailed = 0;

                foreach (var match in matches)
                {
                    try
                    {
                        var result = await SyncMatchLineupsAsync(match.ApiFixtureId.Value);

                        var resultType = result.GetType();
                        var statusProp = resultType.GetProperty("status");
                        var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                        if (isSuccess)
                        {
                            var dataProp = resultType.GetProperty("data");
                            if (dataProp != null)
                            {
                                var data = dataProp.GetValue(result);
                                var dataType = data?.GetType();

                                var addedProp = dataType?.GetProperty("added");
                                var updatedProp = dataType?.GetProperty("updated");

                                if (addedProp != null)
                                    totalAdded += (int)addedProp.GetValue(data);
                                if (updatedProp != null)
                                    totalUpdated += (int)updatedProp.GetValue(data);
                            }
                        }
                        else
                        {
                            totalFailed++;
                        }

                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        totalFailed++;
                        _logger.LogWarning(ex, "Failed to sync lineup for match {ApiFixtureId}", match.ApiFixtureId);
                    }
                }

                return new
                {
                    status = true,
                    message = $"Fetched lineups for {league.LeagueName} {season.Year}: {totalAdded} added, {totalUpdated} updated, {totalFailed} failed",
                    data = new
                    {
                        added = totalAdded,
                        updated = totalUpdated,
                        failed = totalFailed,
                        totalMatches = matches.Count
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lineups");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncTeamContractsAsync(int apiTeamId)
        {
            try
            {
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);

                if (team == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Team with API ID {apiTeamId} not found. Please sync teams first.",
                        data = (object)null
                    };
                }

                string playersUrl = $"https://www.sofascore.com/api/v1/team/{apiTeamId}/players";
                _logger.LogInformation("Fetching players for team {ApiTeamId}", apiTeamId);
                var playersJson = await FetchJson(playersUrl);
                using var playersDoc = JsonDocument.Parse(playersJson);

                if (!playersDoc.RootElement.TryGetProperty("players", out var players))
                {
                    return new
                    {
                        status = false,
                        message = "No players found in response",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;
                int skipped = 0;

                foreach (var playerEl in players.EnumerateArray())
                {
                    if (!playerEl.TryGetProperty("player", out var player))
                    {
                        skipped++;
                        continue;
                    }

                    if (!player.TryGetProperty("id", out var playerIdEl))
                    {
                        skipped++;
                        continue;
                    }

                    int apiPlayerId = playerIdEl.GetInt32();

                    var dbPlayer = await _context.Players
                        .FirstOrDefaultAsync(p => p.ApiPlayerId == apiPlayerId);

                    if (dbPlayer == null)
                    {
                        _logger.LogWarning("Player with API ID {ApiPlayerId} not found in database", apiPlayerId);
                        skipped++;
                        continue;
                    }

                    DateOnly? endDate = null;
                    if (player.TryGetProperty("contractUntilTimestamp", out var contractUntil))
                    {
                        long timestamp = contractUntil.GetInt64();
                        if (timestamp > 0)
                        {
                            endDate = DateOnly.FromDateTime(
                                DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime);
                        }
                    }

                    DateOnly? startDate = null;

                    try
                    {
                        string transfersUrl = $"https://www.sofascore.com/api/v1/player/{apiPlayerId}/transfer-history";
                        var transfersJson = await FetchJson(transfersUrl);
                        using var transfersDoc = JsonDocument.Parse(transfersJson);

                        if (transfersDoc.RootElement.TryGetProperty("transferHistory", out var transferHistory))
                        {
                            foreach (var transfer in transferHistory.EnumerateArray())
                            {
                                if (transfer.TryGetProperty("transferTo", out var transferTo) &&
                                    transferTo.TryGetProperty("id", out var toTeamIdEl) &&
                                    toTeamIdEl.GetInt32() == apiTeamId)
                                {
                                    if (transfer.TryGetProperty("transferDateTimestamp", out var transferDateTs))
                                    {
                                        long timestamp = transferDateTs.GetInt64();
                                        startDate = DateOnly.FromDateTime(
                                            DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch transfer history for player {ApiPlayerId}", apiPlayerId);
                    }

                    bool? isActive = null;
                    if (endDate.HasValue)
                    {
                        var today = DateOnly.FromDateTime(DateTime.UtcNow);
                        isActive = endDate.Value >= today;
                    }
                    else
                    {
                        isActive = true;
                    }

                    var existingContract = await _context.Contracts
                        .FirstOrDefaultAsync(c => c.PlayerId == dbPlayer.PlayerId &&
                                                  c.TeamId == team.TeamId);

                    if (existingContract == null)
                    {
                        var newContract = new Contract
                        {
                            PlayerId = dbPlayer.PlayerId,
                            TeamId = team.TeamId,
                            StartDate = startDate,
                            EndDate = endDate,
                            IsActive = isActive
                        };

                        _context.Contracts.Add(newContract);
                        added++;
                        _logger.LogDebug("Added contract for player {PlayerName} (Start: {StartDate}, End: {EndDate})",
                            dbPlayer.FullName, startDate, endDate);
                    }
                    else
                    {
                        existingContract.StartDate = startDate ?? existingContract.StartDate;
                        existingContract.EndDate = endDate ?? existingContract.EndDate;
                        existingContract.IsActive = isActive;
                        existingContract.TeamId = team.TeamId;

                        _context.Contracts.Update(existingContract);
                        updated++;
                        _logger.LogDebug("Updated contract for player {PlayerName}", dbPlayer.FullName);
                    }
                }

                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = $"Synced contracts for team {team.TeamName}: {added} added, {updated} updated, {skipped} skipped",
                    data = new
                    {
                        added,
                        updated,
                        skipped,
                        teamId = team.TeamId,
                        teamName = team.TeamName,
                        apiTeamId = apiTeamId
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing contracts for team {ApiTeamId}", apiTeamId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncTeamTransfersAsync(int apiTeamId)
        {
            try
            {
                var team = await _context.Teams
                    .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);

                if (team == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Team with API ID {apiTeamId} not found. Please sync teams first.",
                        data = (object)null
                    };
                }

                var players = await _context.Players
                    .Where(p => p.TeamId == team.TeamId && p.ApiPlayerId.HasValue)
                    .Select(p => new { p.PlayerId, p.ApiPlayerId, p.FullName })
                    .ToListAsync();

                if (!players.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No players found for team {team.TeamName}. Sync players first.",
                        data = (object)null
                    };
                }

                int added = 0;
                int updated = 0;
                int skipped = 0;

                foreach (var player in players)
                {
                    try
                    {
                        string transfersUrl = $"https://www.sofascore.com/api/v1/player/{player.ApiPlayerId}/transfer-history";
                        var transfersJson = await FetchJson(transfersUrl);
                        using var doc = JsonDocument.Parse(transfersJson);

                        if (!doc.RootElement.TryGetProperty("transferHistory", out var transferHistory))
                        {
                            skipped++;
                            continue;
                        }

                        foreach (var transferEl in transferHistory.EnumerateArray())
                        {
                            if (!transferEl.TryGetProperty("id", out var transferIdEl))
                            {
                                _logger.LogWarning("Transfer without ID for player {PlayerId}", player.PlayerId);
                                continue;
                            }

                            int apiTransferId = transferIdEl.GetInt32();

                            // Use direct team name fields from API
                            string fromTeam = null;
                            string toTeam = null;
                            DateTime? transferDate = null;
                            string transferType = null;
                            string transferFee = null;

                            // Get team names from direct fields
                            if (transferEl.TryGetProperty("fromTeamName", out var fromTeamNameEl))
                            {
                                fromTeam = fromTeamNameEl.GetString();
                            }

                            if (transferEl.TryGetProperty("toTeamName", out var toTeamNameEl))
                            {
                                toTeam = toTeamNameEl.GetString();
                            }

                            // Get transfer date
                            if (transferEl.TryGetProperty("transferDateTimestamp", out var dateTs))
                            {
                                long timestamp = dateTs.GetInt64();
                                transferDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                            }

                            // Get transfer type
                            if (transferEl.TryGetProperty("type", out var typeEl))
                            {
                                int type = typeEl.GetInt32();
                                transferType = type switch
                                {
                                    1 => "Loan",
                                    2 => "Loan Return",
                                    3 => "Transfer",
                                    4 => "Retirement",
                                    _ => "Unknown"
                                };
                            }

                            // Get transfer fee
                            if (transferEl.TryGetProperty("transferFeeDescription", out var feeDescEl))
                            {
                                string feeDesc = feeDescEl.GetString();
                                transferFee = !string.IsNullOrEmpty(feeDesc) && feeDesc != "-" && feeDesc != "Unknown"
                                    ? feeDesc
                                    : null;
                            }

                            if (string.IsNullOrEmpty(transferFee) && transferEl.TryGetProperty("transferFeeRaw", out var feeRaw) &&
                                feeRaw.TryGetProperty("value", out var feeValue))
                            {
                                decimal value = feeValue.GetDecimal();
                                transferFee = value == 0 ? "Free" : $"{value:N0} €";
                            }

                            var existingTransfer = await _context.Transfers
                                .FirstOrDefaultAsync(t => t.ApiTransferId == apiTransferId);

                            if (existingTransfer == null)
                            {
                                var newTransfer = new Transfer
                                {
                                    ApiTransferId = apiTransferId,
                                    PlayerId = player.PlayerId,
                                    FromTeam = fromTeam,
                                    ToTeam = toTeam,
                                    TransferDate = transferDate,
                                    TransferType = transferType,
                                    TransferFee = transferFee
                                };

                                _context.Transfers.Add(newTransfer);
                                added++;
                                _logger.LogDebug("Added transfer {ApiTransferId} for player {PlayerName}",
                                    apiTransferId, player.FullName);
                            }
                            else
                            {
                                existingTransfer.PlayerId = player.PlayerId;
                                existingTransfer.FromTeam = fromTeam ?? existingTransfer.FromTeam;
                                existingTransfer.ToTeam = toTeam ?? existingTransfer.ToTeam;
                                existingTransfer.TransferDate = transferDate ?? existingTransfer.TransferDate;
                                existingTransfer.TransferType = transferType ?? existingTransfer.TransferType;
                                existingTransfer.TransferFee = transferFee ?? existingTransfer.TransferFee;

                                _context.Transfers.Update(existingTransfer);
                                updated++;
                                _logger.LogDebug("Updated transfer {ApiTransferId} for player {PlayerName}",
                                    apiTransferId, player.FullName);
                            }
                        }

                        await _context.SaveChangesAsync();
                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync transfers for player {PlayerId} ({PlayerName})",
                            player.PlayerId, player.FullName);
                        skipped++;
                    }
                }

                return new
                {
                    status = true,
                    message = $"Synced transfers for team {team.TeamName}: {added} added, {updated} updated, {skipped} skipped",
                    data = new
                    {
                        added,
                        updated,
                        skipped,
                        teamId = team.TeamId,
                        teamName = team.TeamName,
                        apiTeamId = apiTeamId
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing transfers for team {ApiTeamId}", apiTeamId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncAllTeamContractsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found for league {league.LeagueName}. Please sync seasons first.",
                        data = (object)null
                    };
                }

                string standingsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/standings/total";
                _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);

                var standingsJson = await FetchJson(standingsUrl);
                using var standingsDoc = JsonDocument.Parse(standingsJson);

                if (!standingsDoc.RootElement.TryGetProperty("standings", out var standings))
                {
                    return new
                    {
                        status = false,
                        message = "No standings data found",
                        data = (object)null
                    };
                }

                var teamApiIds = new List<int>();

                foreach (var standingGroup in standings.EnumerateArray())
                {
                    if (!standingGroup.TryGetProperty("rows", out var rows))
                        continue;

                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.TryGetProperty("team", out var teamEl) &&
                            teamEl.TryGetProperty("id", out var teamIdEl))
                        {
                            int apiTeamId = teamIdEl.GetInt32();
                            if (!teamApiIds.Contains(apiTeamId))
                            {
                                teamApiIds.Add(apiTeamId);
                            }
                        }
                    }
                }

                if (!teamApiIds.Any())
                {
                    return new
                    {
                        status = false,
                        message = "No teams found in standings",
                        data = (object)null
                    };
                }

                _logger.LogInformation("Found {TeamCount} teams in league {LeagueName} season {SeasonYear}",
                    teamApiIds.Count, league.LeagueName, season.Year);

                int totalAdded = 0;
                int totalUpdated = 0;
                int totalSkipped = 0;
                int totalTeamsProcessed = 0;
                var teamResults = new List<object>();

                foreach (int apiTeamId in teamApiIds)
                {
                    try
                    {
                        var team = await _context.Teams
                            .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);

                        if (team == null)
                        {
                            _logger.LogWarning("Team with API ID {ApiTeamId} not found in database", apiTeamId);
                            totalSkipped++;
                            teamResults.Add(new
                            {
                                apiTeamId = apiTeamId,
                                teamName = "Unknown",
                                success = false,
                                message = "Team not found in database"
                            });
                            continue;
                        }

                        var result = await SyncTeamContractsAsync(apiTeamId);

                        var resultType = result.GetType();
                        var statusProp = resultType.GetProperty("status");
                        var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                        if (isSuccess)
                        {
                            var dataProp = resultType.GetProperty("data");
                            var data = dataProp?.GetValue(result);
                            var dataType = data?.GetType();

                            var added = dataType?.GetProperty("added")?.GetValue(data) as int? ?? 0;
                            var updated = dataType?.GetProperty("updated")?.GetValue(data) as int? ?? 0;
                            var skipped = dataType?.GetProperty("skipped")?.GetValue(data) as int? ?? 0;

                            totalAdded += added;
                            totalUpdated += updated;
                            totalSkipped += skipped;
                            totalTeamsProcessed++;

                            teamResults.Add(new
                            {
                                teamId = team.TeamId,
                                teamName = team.TeamName,
                                apiTeamId = apiTeamId,
                                added,
                                updated,
                                skipped,
                                success = true
                            });

                            _logger.LogDebug("Processed contracts for team {TeamName}: +{Added} added, +{Updated} updated, {Skipped} skipped",
                                team.TeamName, added, updated, skipped);
                        }
                        else
                        {
                            var messageProp = resultType.GetProperty("message");
                            var errorMessage = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";

                            teamResults.Add(new
                            {
                                teamId = team.TeamId,
                                teamName = team.TeamName,
                                apiTeamId = apiTeamId,
                                success = false,
                                message = errorMessage
                            });
                            totalSkipped++;
                        }

                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync contracts for team API ID {ApiTeamId}", apiTeamId);
                        totalSkipped++;
                        teamResults.Add(new
                        {
                            apiTeamId = apiTeamId,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return new
                {
                    status = true,
                    message = $"Synced contracts for {totalTeamsProcessed} teams in {league.LeagueName} {season.Year}: " +
                              $"{totalAdded} contracts added, {totalUpdated} updated, {totalSkipped} skipped",
                    data = new
                    {
                        added = totalAdded,
                        updated = totalUpdated,
                        skipped = totalSkipped,
                        totalTeams = teamApiIds.Count,
                        teamsProcessed = totalTeamsProcessed,
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        seasonId = season.SeasonId,
                        seasonYear = season.Year,
                        tournamentId = apiTournamentId,
                        seasonIdParam = apiSeasonId,
                        results = teamResults
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all team contracts for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> SyncAllTeamTransfersByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
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

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found for league {league.LeagueName}. Please sync seasons first.",
                        data = (object)null
                    };
                }

                string standingsUrl = $"https://www.sofascore.com/api/v1/unique-tournament/{apiTournamentId}/season/{apiSeasonId}/standings/total";
                _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);

                var standingsJson = await FetchJson(standingsUrl);
                using var standingsDoc = JsonDocument.Parse(standingsJson);

                if (!standingsDoc.RootElement.TryGetProperty("standings", out var standings))
                {
                    return new
                    {
                        status = false,
                        message = "No standings data found",
                        data = (object)null
                    };
                }

                var teamApiIds = new List<int>();

                foreach (var standingGroup in standings.EnumerateArray())
                {
                    if (!standingGroup.TryGetProperty("rows", out var rows))
                        continue;

                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.TryGetProperty("team", out var teamEl) &&
                            teamEl.TryGetProperty("id", out var teamIdEl))
                        {
                            int apiTeamId = teamIdEl.GetInt32();
                            if (!teamApiIds.Contains(apiTeamId))
                            {
                                teamApiIds.Add(apiTeamId);
                            }
                        }
                    }
                }

                if (!teamApiIds.Any())
                {
                    return new
                    {
                        status = false,
                        message = "No teams found in standings",
                        data = (object)null
                    };
                }

                _logger.LogInformation("Found {TeamCount} teams in league {LeagueName} season {SeasonYear}",
                    teamApiIds.Count, league.LeagueName, season.Year);

                int totalAdded = 0;
                int totalUpdated = 0;
                int totalSkipped = 0;
                int totalTeamsProcessed = 0;
                var teamResults = new List<object>();

                foreach (int apiTeamId in teamApiIds)
                {
                    try
                    {
                        var team = await _context.Teams
                            .FirstOrDefaultAsync(t => t.ApiTeamId == apiTeamId);

                        if (team == null)
                        {
                            _logger.LogWarning("Team with API ID {ApiTeamId} not found in database", apiTeamId);
                            totalSkipped++;
                            teamResults.Add(new
                            {
                                apiTeamId = apiTeamId,
                                teamName = "Unknown",
                                success = false,
                                message = "Team not found in database"
                            });
                            continue;
                        }

                        var result = await SyncTeamTransfersAsync(apiTeamId);

                        var resultType = result.GetType();
                        var statusProp = resultType.GetProperty("status");
                        var isSuccess = statusProp != null && (bool)statusProp.GetValue(result);

                        if (isSuccess)
                        {
                            var dataProp = resultType.GetProperty("data");
                            var data = dataProp?.GetValue(result);
                            var dataType = data?.GetType();

                            var added = dataType?.GetProperty("added")?.GetValue(data) as int? ?? 0;
                            var updated = dataType?.GetProperty("updated")?.GetValue(data) as int? ?? 0;
                            var skipped = dataType?.GetProperty("skipped")?.GetValue(data) as int? ?? 0;

                            totalAdded += added;
                            totalUpdated += updated;
                            totalSkipped += skipped;
                            totalTeamsProcessed++;

                            teamResults.Add(new
                            {
                                teamId = team.TeamId,
                                teamName = team.TeamName,
                                apiTeamId = apiTeamId,
                                added,
                                updated,
                                skipped,
                                success = true
                            });

                            _logger.LogDebug("Processed transfers for team {TeamName}: +{Added} added, +{Updated} updated, {Skipped} skipped",
                                team.TeamName, added, updated, skipped);
                        }
                        else
                        {
                            var messageProp = resultType.GetProperty("message");
                            var errorMessage = messageProp?.GetValue(result)?.ToString() ?? "Unknown error";

                            teamResults.Add(new
                            {
                                teamId = team.TeamId,
                                teamName = team.TeamName,
                                apiTeamId = apiTeamId,
                                success = false,
                                message = errorMessage
                            });
                            totalSkipped++;
                        }

                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync transfers for team API ID {ApiTeamId}", apiTeamId);
                        totalSkipped++;
                        teamResults.Add(new
                        {
                            apiTeamId = apiTeamId,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return new
                {
                    status = true,
                    message = $"Synced transfers for {totalTeamsProcessed} teams in {league.LeagueName} {season.Year}: " +
                              $"{totalAdded} transfers added, {totalUpdated} updated, {totalSkipped} skipped",
                    data = new
                    {
                        added = totalAdded,
                        updated = totalUpdated,
                        skipped = totalSkipped,
                        totalTeams = teamApiIds.Count,
                        teamsProcessed = totalTeamsProcessed,
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        seasonId = season.SeasonId,
                        seasonYear = season.Year,
                        tournamentId = apiTournamentId,
                        seasonIdParam = apiSeasonId,
                        results = teamResults
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all team transfers for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> GetAllTransfersAsync()
        {
            try
            {
                var teamIds = await _context.Teams
                    .Select(t => t.TeamId)
                    .ToListAsync();

                if (!teamIds.Any())
                {
                    return new
                    {
                        status = false,
                        message = "No teams found in the database.",
                        data = (object)null
                    };
                }

                var playerIds = await _context.Players
                    .Where(p => p.TeamId != null && teamIds.Contains(p.TeamId.Value))
                    .Select(p => p.PlayerId)
                    .ToListAsync();

                if (!playerIds.Any())
                {
                    return new
                    {
                        status = false,
                        message = "No players found in the database.",
                        data = (object)null
                    };
                }

                var transfers = await _context.Transfers
                    .Include(t => t.Player)
                    .Where(t => t.PlayerId != null && playerIds.Contains(t.PlayerId.Value))
                    .Select(t => new
                    {
                        t.TransferId,
                        t.ApiTransferId,
                        t.PlayerId,
                        t.FromTeam,
                        t.ToTeam,
                        t.TransferDate,
                        t.TransferType,
                        t.TransferFee,
                        Player = t.Player != null ? new
                        {
                            t.Player.PlayerId,
                            t.Player.FullName,
                            t.Player.Position,
                            t.Player.Number,
                            t.Player.Nationality,
                            t.Player.ApiPlayerId
                        } : null
                    })
                    .OrderByDescending(t => t.TransferDate)
                    .ToListAsync();

                var teamNames = await _context.Teams
                    .Select(t => t.TeamName)
                    .ToListAsync();

                var transfersByPlayer = transfers
                    .Where(t => t.Player != null)
                    .GroupBy(t => new { t.Player.PlayerId, t.Player.FullName, t.Player.Position, t.Player.Number, t.Player.Nationality, t.Player.ApiPlayerId })
                    .Select(g => new
                    {
                        playerId = g.Key.PlayerId,
                        playerName = g.Key.FullName,
                        playerPosition = g.Key.Position,
                        playerNumber = g.Key.Number,
                        playerNationality = g.Key.Nationality,
                        apiPlayerId = g.Key.ApiPlayerId,
                        transferHistory = g.Select(t => new
                        {
                            transferId = t.TransferId,
                            apiTransferId = t.ApiTransferId,
                            transferDate = t.TransferDate,
                            transferType = t.TransferType,
                            transferFee = t.TransferFee,
                            fromTeam = t.FromTeam,
                            toTeam = t.ToTeam
                        }).OrderByDescending(t => t.transferDate).ToList()
                    })
                    .OrderBy(p => p.playerName)
                    .ToList();

                var transfersInByTeam = transfers
                    .Where(t => !string.IsNullOrEmpty(t.ToTeam) && teamNames.Contains(t.ToTeam))
                    .GroupBy(t => t.ToTeam)
                    .Select(g => new
                    {
                        teamName = g.Key,
                        transfersIn = g.Count(),
                        players = g.Select(t => new
                        {
                            playerId = t.PlayerId,
                            playerName = t.Player != null ? t.Player.FullName : "Unknown",
                            fromTeam = t.FromTeam,
                            transferDate = t.TransferDate,
                            transferType = t.TransferType,
                            transferFee = t.TransferFee
                        }).OrderByDescending(t => t.transferDate).ToList()
                    })
                    .OrderBy(t => t.teamName)
                    .ToList();

                var transfersOutByTeam = transfers
                    .Where(t => !string.IsNullOrEmpty(t.FromTeam) && teamNames.Contains(t.FromTeam))
                    .GroupBy(t => t.FromTeam)
                    .Select(g => new
                    {
                        teamName = g.Key,
                        transfersOut = g.Count(),
                        players = g.Select(t => new
                        {
                            playerId = t.PlayerId,
                            playerName = t.Player != null ? t.Player.FullName : "Unknown",
                            toTeam = t.ToTeam,
                            transferDate = t.TransferDate,
                            transferType = t.TransferType,
                            transferFee = t.TransferFee
                        }).OrderByDescending(t => t.transferDate).ToList()
                    })
                    .OrderBy(t => t.teamName)
                    .ToList();

                int totalTransfers = transfers.Count;
                int transfersIn = transfers.Count(t => !string.IsNullOrEmpty(t.ToTeam) && teamNames.Contains(t.ToTeam));
                int transfersOut = transfers.Count(t => !string.IsNullOrEmpty(t.FromTeam) && teamNames.Contains(t.FromTeam));
                int transfersWithFee = transfers.Count(t => !string.IsNullOrEmpty(t.TransferFee) && t.TransferFee != "-" && t.TransferFee != "Unknown");
                int loans = transfers.Count(t => t.TransferType == "Loan");
                int permanentTransfers = transfers.Count(t => t.TransferType == "Transfer");

                return new
                {
                    status = true,
                    message = $"Retrieved {totalTransfers} total transfers from the database",
                    data = new
                    {
                        summary = new
                        {
                            totalTransfers,
                            transfersIn,
                            transfersOut,
                            transfersWithFee,
                            loans,
                            permanentTransfers,
                            totalPlayersWithTransfers = transfersByPlayer.Count,
                            totalTeamsWithTransfersIn = transfersInByTeam.Count,
                            totalTeamsWithTransfersOut = transfersOutByTeam.Count
                        },
                        transfersByPlayer,
                        transfersInByTeam,
                        transfersOutByTeam
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transfers");
                return new
                {
                    status = false,
                    message = ex.Message,
                    data = ex.StackTrace
                };
            }
        }

        public async Task<object> GetContractsByLeagueSeasonAsync(int apiTournamentId, int apiSeasonId)
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
                        message = $"League with API ID {apiTournamentId} not found.",
                        data = (object)null
                    };
                }

                var season = await _context.Seasons
                    .FirstOrDefaultAsync(s => s.ApiSeasonId == apiSeasonId && s.LeagueId == league.LeagueId);

                if (season == null)
                {
                    return new
                    {
                        status = false,
                        message = $"Season with API ID {apiSeasonId} not found for league {league.LeagueName}.",
                        data = (object)null
                    };
                }

                var teamIds = await _context.Standings
                    .Where(s => s.LeagueId == league.LeagueId && s.SeasonId == season.SeasonId && s.TeamId != null)
                    .Select(s => s.TeamId)
                    .Distinct()
                    .ToListAsync();

                if (!teamIds.Any())
                {                var homeTeamIds = await _context.Matches
                        .Where(m => m.LeagueId == league.LeagueId && m.SeasonId == season.SeasonId && m.HomeTeamId != null)
                        .Select(m => m.HomeTeamId)
                        .Distinct()
                        .ToListAsync();

                    var awayTeamIds = await _context.Matches
                        .Where(m => m.LeagueId == league.LeagueId && m.SeasonId == season.SeasonId && m.AwayTeamId != null)
                        .Select(m => m.AwayTeamId)
                        .Distinct()
                        .ToListAsync();

                    teamIds = homeTeamIds.Concat(awayTeamIds).Distinct().ToList();
                }

                if (!teamIds.Any())
                {
                    return new
                    {
                        status = false,
                        message = $"No teams found for {league.LeagueName} season {season.Year}",
                        data = (object)null
                    };
                }

                var contractsQuery = from c in _context.Contracts
                                     join t in _context.Teams on c.TeamId equals t.TeamId
                                     join p in _context.Players on c.PlayerId equals p.PlayerId
                                     where teamIds.Contains(c.TeamId)
                                     select new
                                     {
                                         c.ContractId,
                                         c.PlayerId,
                                         c.TeamId,
                                         c.StartDate,
                                         c.EndDate,
                                         c.IsActive,
                                         Player = new
                                         {
                                             p.PlayerId,
                                             p.FullName,
                                             p.Position,
                                             p.Number,
                                             p.Nationality,
                                             p.PhotoUrl,
                                             p.DateOfBirth,
                                             p.HeightCm,
                                             p.ApiPlayerId
                                         },
                                         Team = new
                                         {
                                             t.TeamId,
                                             t.TeamName,
                                             t.ShortName,
                                             t.LogoUrl,
                                             t.ApiTeamId
                                         }
                                     };

                var contracts = await contractsQuery.ToListAsync();

                var orderedContracts = contracts
                    .OrderBy(c => c.Team.TeamName)
                    .ThenBy(c => c.Player.FullName)
                    .ToList();

                var contractsByTeam = orderedContracts
                    .GroupBy(c => new { c.Team.TeamId, c.Team.TeamName, c.Team.ShortName, c.Team.LogoUrl, c.Team.ApiTeamId })
                    .Select(g => new
                    {
                        teamId = g.Key.TeamId,
                        teamName = g.Key.TeamName,
                        shortName = g.Key.ShortName,
                        logoUrl = g.Key.LogoUrl,
                        apiTeamId = g.Key.ApiTeamId,
                        contracts = g.Select(c => new
                        {
                            contractId = c.ContractId,
                            playerId = c.PlayerId,
                            playerName = c.Player.FullName,
                            playerPosition = c.Player.Position,
                            playerNumber = c.Player.Number,
                            playerNationality = c.Player.Nationality,
                            playerPhotoUrl = c.Player.PhotoUrl,
                            playerDateOfBirth = c.Player.DateOfBirth,
                            playerHeightCm = c.Player.HeightCm,
                            apiPlayerId = c.Player.ApiPlayerId,
                            startDate = c.StartDate,
                            endDate = c.EndDate,
                            isActive = c.IsActive,
                            contractStatus = c.IsActive == true ? "Active" : (c.IsActive == false ? "Expired" : "Unknown")
                        }).OrderBy(c => c.playerName).ToList()
                    })
                    .OrderBy(g => g.teamName)
                    .ToList();

                int totalContracts = contracts.Count;
                int activeContracts = contracts.Count(c => c.IsActive == true);
                int expiredContracts = contracts.Count(c => c.IsActive == false);
                int contractsWithoutEndDate = contracts.Count(c => !c.EndDate.HasValue);

                return new
                {
                    status = true,
                    message = $"Retrieved {totalContracts} contracts for {league.LeagueName} season {season.Year}",
                    data = new
                    {
                        leagueId = league.LeagueId,
                        leagueName = league.LeagueName,
                        seasonId = season.SeasonId,
                        seasonYear = season.Year,
                        tournamentId = apiTournamentId,
                        seasonIdParam = apiSeasonId,
                        summary = new
                        {
                            totalContracts,
                            activeContracts,
                            expiredContracts,
                            contractsWithoutEndDate,
                            totalTeams = contractsByTeam.Count
                        },
                        contractsByTeam
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contracts for tournament {TournamentId}, season {SeasonId}",
                    apiTournamentId, apiSeasonId);
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