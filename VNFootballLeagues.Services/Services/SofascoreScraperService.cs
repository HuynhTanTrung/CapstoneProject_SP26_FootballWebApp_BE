using PuppeteerSharp;
using VNFootballLeagues.Services.IServices;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace VNFootballLeagues.Services.Services;

/// <summary>
/// Service for scraping SofaScore data using PuppeteerSharp browser automation
/// Bypasses SSL/anti-bot protection by using a real browser instance
/// </summary>
public class SofascoreScraperService : ISofascoreScraperService
{
    private readonly ILogger<SofascoreScraperService> _logger;
    private static bool _browserDownloaded = false;
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);
    private static readonly SemaphoreSlim _scrapeLock = new(1, 1); // Only 1 scrape at a time
    private static readonly Dictionary<string, (string Data, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public SofascoreScraperService(ILogger<SofascoreScraperService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fetches match lineup data from SofaScore API using browser automation
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON string containing lineup data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetMatchLineupsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/lineups";
        _logger.LogInformation("Fetching lineups for event {EventId}", eventId);
        
        return await ScrapeApiEndpointAsync(url, $"event {eventId} lineups");
    }

    /// <summary>
    /// Fetches tournament standings data from SofaScore API using browser automation
    /// </summary>
    /// <param name="tournamentId">The SofaScore uniqueTournament ID (e.g. 626 for V-League 1)</param>
    /// <param name="seasonId">The season ID</param>
    /// <returns>JSON string containing standings data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/standings/total";
        _logger.LogInformation("Fetching standings for tournament {TournamentId}, season {SeasonId}", tournamentId, seasonId);
        
        return await ScrapeApiEndpointAsync(url, $"tournament {tournamentId} season {seasonId} standings");
    }

    /// <summary>
    /// Fetches live matches currently in progress
    /// </summary>
    /// <returns>JSON string containing live matches data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetLiveMatchesAsync()
    {
        string url = "https://www.sofascore.com/api/v1/sport/football/events/live";
        _logger.LogInformation("Fetching live matches");
        
        return await ScrapeApiEndpointAsync(url, "live matches");
    }

    /// <summary>
    /// Fetches live and upcoming matches for Vietnamese leagues only
    /// Filters for V-League 1 (626), V-League 2 (771), and Vietnam Cup (3087)
    /// </summary>
    /// <returns>JSON string containing filtered matches data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetVietnameseLeagueLiveMatchesAsync()
    {
        try
        {
            // Get all live matches
            string allLiveMatchesJson = await GetLiveMatchesAsync();
            var allMatches = JsonSerializer.Deserialize<JsonElement>(allLiveMatchesJson);

            // Vietnamese uniqueTournament IDs (not tournament.id!)
            var vietnameseTournamentIds = new HashSet<int> { 626, 771, 3087 }; // V-League 1, V-League 2, Vietnam Cup

            // Filter matches by uniqueTournament ID
            var filteredEvents = new List<JsonElement>();
            
            if (allMatches.TryGetProperty("events", out var events))
            {
                foreach (var match in events.EnumerateArray())
                {
                    // Check tournament.uniqueTournament.id instead of tournament.id
                    if (match.TryGetProperty("tournament", out var tournament) &&
                        tournament.TryGetProperty("uniqueTournament", out var uniqueTournament) &&
                        uniqueTournament.TryGetProperty("id", out var uniqueTournamentId))
                    {
                        if (vietnameseTournamentIds.Contains(uniqueTournamentId.GetInt32()))
                        {
                            filteredEvents.Add(match);
                        }
                    }
                }
            }

            // Build filtered response
            var filteredResponse = new
            {
                events = filteredEvents,
                count = filteredEvents.Count,
                message = filteredEvents.Count == 0 
                    ? "No Vietnamese league matches currently live" 
                    : $"Found {filteredEvents.Count} Vietnamese league match(es)"
            };

            string filteredJson = JsonSerializer.Serialize(filteredResponse, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            _logger.LogInformation("Found {Count} Vietnamese league matches", filteredEvents.Count);
            return filteredJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Vietnamese league live matches");
            throw new Exception($"Failed to fetch Vietnamese league live matches: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Fetches match incidents (goals, cards, substitutions) for a specific event
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON string containing incidents data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetMatchIncidentsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/incidents";
        _logger.LogInformation("Fetching incidents for event {EventId}", eventId);
        
        return await ScrapeApiEndpointAsync(url, $"event {eventId} incidents");
    }

    public async Task<string> GetMatchDetailsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}";
        _logger.LogInformation("Fetching match details for event {EventId}", eventId);

        return await ScrapeApiEndpointAsync(url, $"event {eventId} details");
    }

    /// <summary>
    /// Fetches last (previous) matches for a tournament
    /// </summary>
    public async Task<string> GetTournamentLastMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/last/{page}";
        _logger.LogInformation("Fetching last matches for tournament {TournamentId}, season {SeasonId}, page {Page}", 
            uniqueTournamentId, seasonId, page);
        
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} last matches");
    }

    /// <summary>
    /// Fetches next (upcoming) matches for a tournament
    /// </summary>
    public async Task<string> GetTournamentNextMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/next/{page}";
        _logger.LogInformation("Fetching next matches for tournament {TournamentId}, season {SeasonId}, page {Page}", 
            uniqueTournamentId, seasonId, page);
        
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} next matches");
    }

    /// <summary>
    /// Fetches matches for a specific round in a tournament
    /// </summary>
    public async Task<string> GetTournamentRoundMatchesAsync(int uniqueTournamentId, int seasonId, int round)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/round/{round}";
        _logger.LogInformation("Fetching round {Round} matches for tournament {TournamentId}, season {SeasonId}", 
            round, uniqueTournamentId, seasonId);
        
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} round {round}");
    }

    /// <summary>
    /// Fetches last (previous) matches for a team
    /// </summary>
    public async Task<string> GetTeamLastMatchesAsync(int teamId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/events/last/{page}";
        _logger.LogInformation("Fetching last matches for team {TeamId}, page {Page}", teamId, page);
        
        return await ScrapeApiEndpointAsync(url, $"team {teamId} last matches");
    }

    /// <summary>
    /// Fetches next (upcoming) matches for a team
    /// </summary>
    public async Task<string> GetTeamNextMatchesAsync(int teamId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/events/next/{page}";
        _logger.LogInformation("Fetching next matches for team {TeamId}, page {Page}", teamId, page);
        
        return await ScrapeApiEndpointAsync(url, $"team {teamId} next matches");
    }

    public async Task<string> GetTeamDetailsAsync(int teamId)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}";
        _logger.LogInformation("Fetching details for team {TeamId}", teamId);
        return await ScrapeApiEndpointAsync(url, $"team {teamId} details");
    }

    public async Task<string> GetTeamPlayersAsync(int teamId)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/players";
        _logger.LogInformation("Fetching players for team {TeamId}", teamId);
        return await ScrapeApiEndpointAsync(url, $"team {teamId} players");
    }

    public async Task<string> GetTournamentCupTreesAsync(int uniqueTournamentId, int seasonId)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/cuptrees";
        _logger.LogInformation("Fetching cup trees for tournament {TournamentId} season {SeasonId}", uniqueTournamentId, seasonId);
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} season {seasonId} cuptrees");
    }

    public async Task<string> GetPlayerMatchStatisticsAsync(int eventId, int playerId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/player/{playerId}/statistics";
        _logger.LogInformation("Fetching player {PlayerId} statistics for event {EventId}", playerId, eventId);
        return await ScrapeApiEndpointAsync(url, $"event {eventId} player {playerId} statistics");
    }

    /// <summary>
    /// Generic method to scrape any SofaScore API endpoint
    /// </summary>
    /// <param name="apiUrl">The full API URL to scrape</param>
    /// <param name="description">Description for logging purposes</param>
    /// <returns>JSON string response</returns>
    private async Task<string> ScrapeApiEndpointAsync(string apiUrl, string description)
    {
        // Check cache first
        lock (_cache)
        {
            if (_cache.TryGetValue(apiUrl, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogInformation("Cache hit for {Description}", description);
                return cached.Data;
            }
        }

        // Serialize scrape requests - only 1 at a time
        await _scrapeLock.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock
            lock (_cache)
            {
                if (_cache.TryGetValue(apiUrl, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                    return cached.Data;
            }

            var result = await ScrapeApiEndpointInternalAsync(apiUrl, description);

            lock (_cache)
            {
                _cache[apiUrl] = (result, DateTime.UtcNow.Add(_cacheDuration));
            }

            return result;
        }
        finally
        {
            _scrapeLock.Release();
        }
    }

    private async Task<string> ScrapeApiEndpointInternalAsync(string apiUrl, string description)
    {
        IBrowser? browser = null;
        IPage? page = null;

        try
        {
            await EnsureBrowserDownloadedAsync();

            _logger.LogInformation("Launching headless browser for {Description}", description);
            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-accelerated-2d-canvas",
                    "--disable-gpu",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-features=IsolateOrigins,site-per-process"
                }
            });

            page = await browser.NewPageAsync();

            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });
            await page.SetUserAgentAsync(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            );
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                { "Accept-Language", "en-US,en;q=0.9" },
            });

            // Visit trang chủ để lấy cookies/session
            _logger.LogInformation("Establishing session by visiting main site");
            await page.GoToAsync("https://www.sofascore.com/", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 60000
            });
            await Task.Delay(1500);

            // Fetch API bằng JS fetch trong browser context (có cookies)
            _logger.LogInformation("Fetching data from: {Url}", apiUrl);
            string jsonResponse = await page.EvaluateFunctionAsync<string>($@"
                async () => {{
                    const r = await fetch('{apiUrl}', {{
                        headers: {{ 'Accept': 'application/json' }},
                        credentials: 'include'
                    }});
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    return await r.text();
                }}
            ");

            _logger.LogInformation("Successfully retrieved data for {Description} ({Length} characters)", description, jsonResponse.Length);
            return jsonResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping data for {Description}", description);
            throw new Exception($"Failed to scrape {description}: {ex.Message}", ex);
        }
        finally
        {
            if (page != null) try { await page.CloseAsync(); } catch { }
            if (browser != null) try { await browser.CloseAsync(); } catch { }
        }
    }

    /// <summary>
    /// Ensures Chromium browser is downloaded before first use
    /// Thread-safe implementation to prevent multiple simultaneous downloads
    /// </summary>
    private async Task EnsureBrowserDownloadedAsync()
    {
        if (_browserDownloaded)
        {
            return;
        }

        await _downloadLock.WaitAsync();
        try
        {
            if (_browserDownloaded)
            {
                return;
            }

            _logger.LogInformation("Downloading Chromium browser (first run only)...");

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            _browserDownloaded = true;
            _logger.LogInformation("Chromium browser downloaded successfully");
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
