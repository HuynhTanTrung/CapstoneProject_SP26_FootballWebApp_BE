using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models;
using VNFootballLeaguesApp.Hubs;

namespace VNFootballLeaguesApp.Services;

/// <summary>
/// Background service that polls SofaScore incidents for manually tracked matches
/// Poll interval: 60s for each tracked match
/// Matches are added/removed via controller endpoints (manual control)
/// </summary>
public class LiveMatchPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<LiveMatchHub> _hubContext;
    private readonly ILogger<LiveMatchPollingService> _logger;
    
    // Thread-safe collections for tracking matches
    private static readonly ConcurrentDictionary<int, string> _matchCache = new();
    private static readonly ConcurrentDictionary<int, MatchInfo> _matchInfoCache = new();
    private static readonly ConcurrentDictionary<int, Dictionary<int, string>> _playerNamesCache = new();
    private static readonly ConcurrentBag<int> _trackedMatches = new();

    private class MatchInfo
    {
        public string? HomeTeam { get; set; }
        public string? AwayTeam { get; set; }
        public string? Status { get; set; }
    }

    public LiveMatchPollingService(
        IServiceProvider serviceProvider,
        IHubContext<LiveMatchHub> hubContext,
        ILogger<LiveMatchPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Add a match to tracking list
    /// </summary>
    public static void AddMatch(int eventId)
    {
        if (!_trackedMatches.Contains(eventId))
        {
            _trackedMatches.Add(eventId);
        }
    }

    /// <summary>
    /// Remove a match from tracking list
    /// </summary>
    public static void RemoveMatch(int eventId)
    {
        var list = _trackedMatches.ToList();
        _trackedMatches.Clear();
        foreach (var id in list.Where(id => id != eventId))
        {
            _trackedMatches.Add(id);
        }
        _matchCache.TryRemove(eventId, out _);
    }

    /// <summary>
    /// Get list of currently tracked matches
    /// </summary>
    public static List<int> GetTrackedMatches()
    {
        return _trackedMatches.ToList();
    }

    /// <summary>
    /// Clear all tracked matches
    /// </summary>
    public static void ClearAllMatches()
    {
        _trackedMatches.Clear();
        _matchCache.Clear();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Live Match Polling Service started (Manual control mode)");
        _logger.LogInformation("Use /api/sofascore/tracking endpoints to add/remove matches");

        // Wait a bit before starting to ensure app is fully initialized
        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var trackedMatches = _trackedMatches.ToList();

                if (trackedMatches.Count == 0)
                {
                    _logger.LogDebug("No matches being tracked. Waiting 60s...");
                }
                else
                {
                    _logger.LogInformation("Polling {Count} tracked matches", trackedMatches.Count);
                    await PollTrackedMatchesAsync(trackedMatches, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling cycle");
            }

            // Wait 60s before next poll cycle
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Live Match Polling Service stopped");
    }

    /// <summary>
    /// Poll incidents for all tracked matches concurrently
    /// </summary>
    private async Task PollTrackedMatchesAsync(List<int> eventIds, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var scraperService = scope.ServiceProvider.GetRequiredService<ISofascoreScraperService>();

        // Poll all matches concurrently
        var tasks = eventIds.Select(eventId => 
            PollMatchIncidentsAsync(scraperService, eventId, stoppingToken)
        );

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Poll incidents for a specific match and broadcast updates if changed
    /// </summary>
    private async Task PollMatchIncidentsAsync(
        ISofascoreScraperService scraperService,
        int eventId,
        CancellationToken stoppingToken)
    {
        try
        {
            // Fetch lineups first time to cache player names
            if (!_playerNamesCache.ContainsKey(eventId))
            {
                await CachePlayerNamesAsync(scraperService, eventId);
            }

            // Fetch incidents
            string incidentsJson = await scraperService.GetMatchIncidentsAsync(eventId);

            // Check if data has changed
            if (_matchCache.TryGetValue(eventId, out var cachedData) && cachedData == incidentsJson)
            {
                _logger.LogDebug("No changes for match {EventId}, skipping broadcast", eventId);
                return;
            }

            // Update cache
            _matchCache[eventId] = incidentsJson;

            // Parse incidents
            var incidentsData = JsonSerializer.Deserialize<JsonElement>(incidentsJson);
            
            // Build update with full match info
            var update = new LiveMatchUpdate
            {
                EventId = eventId,
                UpdatedAt = DateTime.UtcNow,
                RecentIncidents = new List<IncidentUpdate>()
            };

            // Get player names cache
            _playerNamesCache.TryGetValue(eventId, out var playerNames);

            // Parse incidents array
            if (incidentsData.TryGetProperty("incidents", out var incidents))
            {
                bool matchFinished = false;

                foreach (var incident in incidents.EnumerateArray())
                {
                    // Check if match finished
                    if (incident.TryGetProperty("incidentType", out var incType) && 
                        incType.GetString() == "period" &&
                        incident.TryGetProperty("text", out var text) && 
                        text.GetString() == "FT")
                    {
                        matchFinished = true;
                    }

                    string? playerName = null;
                    
                    // Try to get player name from incident
                    if (incident.TryGetProperty("player", out var player) && 
                        player.TryGetProperty("name", out var pName))
                    {
                        playerName = pName.GetString();
                    }
                    // If not in incident, try to get from lineup cache using player ID
                    else if (incident.TryGetProperty("player", out var playerObj) && 
                             playerObj.TryGetProperty("id", out var playerId) &&
                             playerNames != null)
                    {
                        playerNames.TryGetValue(playerId.GetInt32(), out playerName);
                    }

                    var incidentUpdate = new IncidentUpdate
                    {
                        Type = incident.TryGetProperty("incidentType", out var type) ? type.GetString() : null,
                        Time = incident.TryGetProperty("time", out var time) ? time.GetInt32() : null,
                        Player = playerName,
                        Team = incident.TryGetProperty("isHome", out var isHome) ? 
                               (isHome.GetBoolean() ? "home" : "away") : null,
                        IncidentClass = incident.TryGetProperty("incidentClass", out var incClass) ? 
                                       incClass.GetString() : null,
                        Length = incident.TryGetProperty("length", out var length) ? length.GetInt32() : null
                    };

                    update.RecentIncidents.Add(incidentUpdate);
                    
                    // Update current minute from latest incident
                    if (incidentUpdate.Time.HasValue && 
                        (!update.CurrentMinute.HasValue || incidentUpdate.Time.Value > update.CurrentMinute.Value))
                    {
                        update.CurrentMinute = incidentUpdate.Time.Value;
                    }
                }

                // Auto-remove match if finished
                if (matchFinished)
                {
                    _logger.LogInformation("Match {EventId} finished, removing from tracking", eventId);
                    RemoveMatch(eventId);
                    update.Status = "finished";
                }
            }

            // Calculate score from goal incidents
            var goals = update.RecentIncidents.Where(i => i.Type == "goal").ToList();
            update.HomeScore = goals.Count(g => g.Team == "home");
            update.AwayScore = goals.Count(g => g.Team == "away");

            // Get cached match info if available
            if (_matchInfoCache.TryGetValue(eventId, out var matchInfo))
            {
                update.HomeTeam = matchInfo.HomeTeam;
                update.AwayTeam = matchInfo.AwayTeam;
                update.Status = matchInfo.Status;
            }

            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", update, stoppingToken);

            _logger.LogInformation("Broadcasted update for match {EventId} - Score: {Home}:{Away}", 
                eventId, update.HomeScore, update.AwayScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling incidents for match {EventId}", eventId);
        }
    }

    /// <summary>
    /// Cache player names from lineups API for player ID lookup
    /// </summary>
    private async Task CachePlayerNamesAsync(ISofascoreScraperService scraperService, int eventId)
    {
        try
        {
            _logger.LogInformation("Caching player names for match {EventId}", eventId);
            
            string lineupsJson = await scraperService.GetMatchLineupsAsync(eventId);
            var lineupsData = JsonSerializer.Deserialize<JsonElement>(lineupsJson);
            
            var playerNames = new Dictionary<int, string>();

            // Parse home team lineup
            if (lineupsData.TryGetProperty("home", out var home) &&
                home.TryGetProperty("players", out var homePlayers))
            {
                foreach (var player in homePlayers.EnumerateArray())
                {
                    if (player.TryGetProperty("player", out var playerObj) &&
                        playerObj.TryGetProperty("id", out var playerId) &&
                        playerObj.TryGetProperty("name", out var playerName))
                    {
                        playerNames[playerId.GetInt32()] = playerName.GetString() ?? "Unknown";
                    }
                }
            }

            // Parse away team lineup
            if (lineupsData.TryGetProperty("away", out var away) &&
                away.TryGetProperty("players", out var awayPlayers))
            {
                foreach (var player in awayPlayers.EnumerateArray())
                {
                    if (player.TryGetProperty("player", out var playerObj) &&
                        playerObj.TryGetProperty("id", out var playerId) &&
                        playerObj.TryGetProperty("name", out var playerName))
                    {
                        playerNames[playerId.GetInt32()] = playerName.GetString() ?? "Unknown";
                    }
                }
            }

            _playerNamesCache[eventId] = playerNames;
            _logger.LogInformation("Cached {Count} player names for match {EventId}", playerNames.Count, eventId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache player names for match {EventId}", eventId);
            // Continue without player names cache
        }
    }

    /// <summary>
    /// Cache match info when adding to tracking
    /// </summary>
    public static void AddMatchWithInfo(int eventId, string homeTeam, string awayTeam, string status)
    {
        AddMatch(eventId);
        _matchInfoCache[eventId] = new MatchInfo
        {
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Status = status
        };
    }
}
