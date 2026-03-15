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
    /// <param name="tournamentId">The SofaScore tournament ID</param>
    /// <param name="seasonId">The season ID</param>
    /// <returns>JSON string containing standings data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId)
    {
        string url = $"https://www.sofascore.com/api/v1/tournament/{tournamentId}/season/{seasonId}/standings/total";
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

    /// <summary>
    /// Generic method to scrape any SofaScore API endpoint
    /// </summary>
    /// <param name="apiUrl">The full API URL to scrape</param>
    /// <param name="description">Description for logging purposes</param>
    /// <returns>JSON string response</returns>
    private async Task<string> ScrapeApiEndpointAsync(string apiUrl, string description)
    {
        IBrowser? browser = null;
        IPage? page = null;

        try
        {
            // Ensure Chromium is downloaded (only happens once)
            await EnsureBrowserDownloadedAsync();

            // Launch headless Chromium browser with stealth options
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

            // Create a new page
            page = await browser.NewPageAsync();

            // Set realistic viewport
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1920,
                Height = 1080
            });

            // Set user agent to mimic a real browser
            await page.SetUserAgentAsync(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );

            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                { "Accept", "application/json, text/plain, */*" },
                { "Accept-Language", "en-US,en;q=0.9" },
                { "Accept-Encoding", "gzip, deflate, br" }
            });

            // First visit main site to establish session and cookies
            _logger.LogInformation("Establishing session by visiting main site");
            await page.GoToAsync("https://www.sofascore.com/", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 30000
            });

            // Wait for page to settle and cookies to be set
            await Task.Delay(2000);

            // Fetch API data using JavaScript fetch within browser context
            _logger.LogInformation("Fetching data from: {Url}", apiUrl);
            string jsonResponse = await page.EvaluateFunctionAsync<string>($@"
                async () => {{
                    try {{
                        const response = await fetch('{apiUrl}', {{
                            method: 'GET',
                            headers: {{
                                'Accept': 'application/json',
                                'Accept-Language': 'en-US,en;q=0.9'
                            }},
                            credentials: 'include'
                        }});
                        
                        if (!response.ok) {{
                            throw new Error(`HTTP ${{response.status}}: ${{response.statusText}}`);
                        }}
                        
                        const data = await response.text();
                        return data;
                    }} catch (error) {{
                        throw new Error(`Fetch failed: ${{error.message}}`);
                    }}
                }}
            ");

            // Validate we got JSON
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                throw new Exception("No content retrieved from the API");
            }

            // Basic JSON validation
            if (!jsonResponse.TrimStart().StartsWith("{") && !jsonResponse.TrimStart().StartsWith("["))
            {
                _logger.LogWarning("Response doesn't appear to be JSON: {Response}", 
                    jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length)));
                throw new Exception("Response is not valid JSON");
            }

            _logger.LogInformation("Successfully retrieved data for {Description} ({Length} characters)", 
                description, jsonResponse.Length);

            return jsonResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping data for {Description}", description);
            throw new Exception($"Failed to scrape {description}: {ex.Message}", ex);
        }
        finally
        {
            // Clean up resources
            if (page != null)
            {
                try { await page.CloseAsync(); } catch { /* Ignore cleanup errors */ }
            }

            if (browser != null)
            {
                try { await browser.CloseAsync(); } catch { /* Ignore cleanup errors */ }
            }
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
