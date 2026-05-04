using VNFootballLeagues.Services.IServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Net.Http;

namespace VNFootballLeagues.Services.Services;

/// <summary>
/// Service for fetching SofaScore data via ScraperAPI (bypasses IP blocks)
/// </summary>
public class SofascoreScraperService : ISofascoreScraperService
{
    private readonly ILogger<SofascoreScraperService> _logger;
    private readonly string _scraperApiKey;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private static readonly SemaphoreSlim _scrapeLock = new(3, 3); // Allow 3 concurrent requests
    private static readonly Dictionary<string, (string Data, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    public SofascoreScraperService(ILogger<SofascoreScraperService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _scraperApiKey = configuration["SofascoreSettings:ScraperApiKey"] ?? throw new InvalidOperationException("ScraperApiKey not configured");
        _maxRetries = int.TryParse(configuration["SofascoreSettings:MaxRetries"], out var r) ? r : 3;
        _retryDelayMs = int.TryParse(configuration["SofascoreSettings:RetryDelayMs"], out var d) ? d : 2000;
    }

    public async Task<string> GetMatchLineupsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/lineups";
        return await ScrapeApiEndpointAsync(url, $"event {eventId} lineups");
    }

    public async Task<string> GetTournamentStandingsAsync(int tournamentId, int seasonId)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{tournamentId}/season/{seasonId}/standings/total";
        return await ScrapeApiEndpointAsync(url, $"tournament {tournamentId} season {seasonId} standings");
    }

    public async Task<string> GetLiveMatchesAsync()
    {
        string url = "https://www.sofascore.com/api/v1/sport/football/events/live";
        return await ScrapeApiEndpointAsync(url, "live matches");
    }

    public async Task<string> GetVietnameseLeagueLiveMatchesAsync()
    {
        try
        {
            string allLiveMatchesJson = await GetLiveMatchesAsync();
            var allMatches = JsonSerializer.Deserialize<JsonElement>(allLiveMatchesJson);
            var vietnameseTournamentIds = new HashSet<int> { 626, 771, 3087 };
            var filteredEvents = new List<JsonElement>();

            if (allMatches.TryGetProperty("events", out var events))
            {
                foreach (var match in events.EnumerateArray())
                {
                    if (match.TryGetProperty("tournament", out var tournament) &&
                        tournament.TryGetProperty("uniqueTournament", out var uniqueTournament) &&
                        uniqueTournament.TryGetProperty("id", out var uniqueTournamentId) &&
                        vietnameseTournamentIds.Contains(uniqueTournamentId.GetInt32()))
                    {
                        filteredEvents.Add(match);
                    }
                }
            }

            var filteredResponse = new
            {
                events = filteredEvents,
                count = filteredEvents.Count,
                message = filteredEvents.Count == 0
                    ? "No Vietnamese league matches currently live"
                    : $"Found {filteredEvents.Count} Vietnamese league match(es)"
            };

            return JsonSerializer.Serialize(filteredResponse, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Vietnamese league live matches");
            throw new Exception($"Failed to fetch Vietnamese league live matches: {ex.Message}", ex);
        }
    }

    public async Task<string> GetMatchIncidentsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/incidents";
        return await ScrapeApiEndpointAsync(url, $"event {eventId} incidents");
    }

    public async Task<string> GetMatchDetailsAsync(int eventId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}";
        return await ScrapeApiEndpointAsync(url, $"event {eventId} details");
    }

    public async Task<string> GetTournamentLastMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/last/{page}";
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} last matches page {page}");
    }

    public async Task<string> GetTournamentNextMatchesAsync(int uniqueTournamentId, int seasonId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/next/{page}";
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} next matches page {page}");
    }

    public async Task<string> GetTournamentRoundMatchesAsync(int uniqueTournamentId, int seasonId, int round)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/events/round/{round}";
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} round {round}");
    }

    public async Task<string> GetTeamLastMatchesAsync(int teamId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/events/last/{page}";
        return await ScrapeApiEndpointAsync(url, $"team {teamId} last matches");
    }

    public async Task<string> GetTeamNextMatchesAsync(int teamId, int page = 0)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/events/next/{page}";
        return await ScrapeApiEndpointAsync(url, $"team {teamId} next matches");
    }

    public async Task<string> GetTeamDetailsAsync(int teamId)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}";
        return await ScrapeApiEndpointAsync(url, $"team {teamId} details");
    }

    public async Task<string> GetTeamPlayersAsync(int teamId)
    {
        string url = $"https://www.sofascore.com/api/v1/team/{teamId}/players";
        return await ScrapeApiEndpointAsync(url, $"team {teamId} players");
    }

    public async Task<string> GetTournamentCupTreesAsync(int uniqueTournamentId, int seasonId)
    {
        string url = $"https://www.sofascore.com/api/v1/unique-tournament/{uniqueTournamentId}/season/{seasonId}/cuptrees";
        return await ScrapeApiEndpointAsync(url, $"tournament {uniqueTournamentId} season {seasonId} cuptrees");
    }

    public async Task<string> GetPlayerMatchStatisticsAsync(int eventId, int playerId)
    {
        string url = $"https://www.sofascore.com/api/v1/event/{eventId}/player/{playerId}/statistics";
        return await ScrapeApiEndpointAsync(url, $"event {eventId} player {playerId} statistics");
    }

    // ─── Core fetch logic ────────────────────────────────────────────────────

    private async Task<string> ScrapeApiEndpointAsync(string apiUrl, string description)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(apiUrl, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogInformation("Cache hit for {Description}", description);
                return cached.Data;
            }
        }

        await _scrapeLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            lock (_cache)
            {
                if (_cache.TryGetValue(apiUrl, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                    return cached.Data;
            }

            var result = await FetchViaScraperApiAsync(apiUrl, description);

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

    private async Task<string> FetchViaScraperApiAsync(string apiUrl, string description)
    {
        var encodedUrl = Uri.EscapeDataString(apiUrl);
        var scraperUrl = $"https://api.scraperapi.com?api_key={_scraperApiKey}&url={encodedUrl}";

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Fetching {Description} via ScraperAPI (attempt {Attempt}/{Max})", description, attempt, _maxRetries);

                var request = new HttpRequestMessage(HttpMethod.Get, scraperUrl);
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ScraperAPI returned {StatusCode} for {Description}", response.StatusCode, description);
                    if (attempt < _maxRetries)
                    {
                        await Task.Delay(_retryDelayMs * attempt);
                        continue;
                    }
                    throw new Exception($"ScraperAPI returned {response.StatusCode} for {description}");
                }

                var content = await response.Content.ReadAsStringAsync();

                // Validate it's JSON
                JsonDocument.Parse(content);

                _logger.LogInformation("Successfully fetched {Description}", description);
                return content;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Non-JSON response for {Description} on attempt {Attempt}", description, attempt);
                if (attempt < _maxRetries)
                {
                    await Task.Delay(_retryDelayMs * attempt);
                    continue;
                }
                throw new Exception($"Invalid JSON response from ScraperAPI for {description}");
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed for {Description}, retrying...", attempt, description);
                await Task.Delay(_retryDelayMs * attempt);
            }
        }

        throw new Exception($"All {_maxRetries} attempts failed for {description}");
    }
}
