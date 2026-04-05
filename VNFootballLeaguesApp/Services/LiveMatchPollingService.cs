using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
    // Matches that detected FT — value is the UTC time when FT was first detected
    private static readonly ConcurrentDictionary<int, DateTime> _pendingFinish = new();
    private static readonly TimeSpan FinishDelay = TimeSpan.FromMinutes(2);

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
        _logger.LogInformation("Live Match Polling Service started");

        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Auto-scan DB for matches that should be live right now
                await AutoScanLiveMatchesAsync(stoppingToken);

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

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Live Match Polling Service stopped");
    }

    /// <summary>
    /// Auto-scan DB for matches that are currently live (within 2 hours of kickoff)
    /// and add them to tracking automatically
    /// </summary>
    private async Task AutoScanLiveMatchesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VNFootballLeagues.Repositories.Models.VNFootballLeaguesDBContext>();

            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-120); // match started up to 2 hours ago
            var windowEnd = now.AddMinutes(5);      // or starts in next 5 minutes

            var liveMatches = await context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.ApiFixtureId != null
                    && m.MatchDate >= windowStart
                    && m.MatchDate <= windowEnd
                    && m.Status != "FT" && m.Status != "finished")
                .ToListAsync(stoppingToken);

            foreach (var match in liveMatches)
            {
                if (match.ApiFixtureId.HasValue && !_trackedMatches.Contains(match.ApiFixtureId.Value))
                {
                    AddMatchWithInfo(
                        match.ApiFixtureId.Value,
                        match.HomeTeam?.TeamName ?? "Home",
                        match.AwayTeam?.TeamName ?? "Away",
                        "live"
                    );
                    _logger.LogInformation("Auto-added match {EventId} ({Home} vs {Away}) to tracking",
                        match.ApiFixtureId.Value, match.HomeTeam?.TeamName, match.AwayTeam?.TeamName);
                }

                // Update status to inprogress in DB so FE shows it as live
                if (match.Status != "inprogress" && match.Status != "1H" && match.Status != "2H" && match.Status != "HT")
                {
                    match.Status = "inprogress";
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during auto-scan for live matches");
        }
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

            // Fetch incidents and match details concurrently
            var incidentsTask = scraperService.GetMatchIncidentsAsync(eventId);
            var matchDetailsTask = scraperService.GetMatchDetailsAsync(eventId);
            await Task.WhenAll(incidentsTask, matchDetailsTask);
            string incidentsJson = incidentsTask.Result;
            string matchDetailsJson = matchDetailsTask.Result;

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
                    
                    // Update current minute from latest incident (fallback)
                    if (incidentUpdate.Time.HasValue && 
                        (!update.CurrentMinute.HasValue || incidentUpdate.Time.Value > update.CurrentMinute.Value))
                    {
                        update.CurrentMinute = incidentUpdate.Time.Value;
                    }
                }

                // Khi detect FT: đánh dấu thời điểm kết thúc, chưa remove ngay
                if (matchFinished)
                {
                    if (!_pendingFinish.ContainsKey(eventId))
                    {
                        _pendingFinish[eventId] = DateTime.UtcNow;
                        _logger.LogInformation("Match {EventId} detected FT, will finalize after {Delay}s", eventId, FinishDelay.TotalSeconds);
                        // Xóa cache để lần poll tiếp theo vẫn fetch lại data mới nhất
                        _matchCache.TryRemove(eventId, out _);
                    }
                    else if (DateTime.UtcNow - _pendingFinish[eventId] >= FinishDelay)
                    {
                        // Đã đủ delay — lần này là poll cuối, sau đó remove
                        _logger.LogInformation("Match {EventId} finalizing after delay, removing from tracking", eventId);
                        _pendingFinish.TryRemove(eventId, out _);
                        RemoveMatch(eventId);
                        update.Status = "finished";
                    }
                    else
                    {
                        // Vẫn trong thời gian delay — tiếp tục poll, xóa cache để lấy data mới
                        _matchCache.TryRemove(eventId, out _);
                    }
                }
            }

            // Calculate score from goal incidents
            var goals = update.RecentIncidents.Where(i => i.Type == "goal").ToList();
            update.HomeScore = goals.Count(g => g.Team == "home");
            update.AwayScore = goals.Count(g => g.Team == "away");

            // Get accurate score and current minute from Sofascore match details
            try
            {
                var matchDetailsData = JsonSerializer.Deserialize<JsonElement>(matchDetailsJson);
                if (matchDetailsData.TryGetProperty("event", out var eventData))
                {
                    // Get score from Sofascore directly (more reliable than counting goals)
                    if (eventData.TryGetProperty("homeScore", out var homeScoreObj) &&
                        homeScoreObj.TryGetProperty("current", out var homeScoreCurrent))
                        update.HomeScore = homeScoreCurrent.GetInt32();
                    if (eventData.TryGetProperty("awayScore", out var awayScoreObj) &&
                        awayScoreObj.TryGetProperty("current", out var awayScoreCurrent))
                        update.AwayScore = awayScoreCurrent.GetInt32();

                    // Get current minute directly from Sofascore time fields
                    if (eventData.TryGetProperty("time", out var timeObj))
                    {
                        // time.played = total seconds played (most accurate)
                        if (timeObj.TryGetProperty("played", out var played))
                        {
                            update.CurrentMinute = (int)Math.Ceiling(played.GetInt32() / 60.0);
                        }
                        // Fallback: currentPeriodStartTimestamp + period offset
                        else if (timeObj.TryGetProperty("currentPeriodStartTimestamp", out var periodStart))
                        {
                            var periodStartTime = DateTimeOffset.FromUnixTimeSeconds(periodStart.GetInt64()).UtcDateTime;
                            var elapsed = (int)Math.Ceiling((DateTime.UtcNow - periodStartTime).TotalMinutes);

                            // Check period from Sofascore statusCode: 6=1H, 7=2H, 31=ET1, 32=ET2
                            int periodOffset = 0;
                            if (eventData.TryGetProperty("statusCode", out var statusCode))
                            {
                                var code = statusCode.GetInt32();
                                if (code == 7) periodOffset = 45;
                                else if (code == 31) periodOffset = 90;
                                else if (code == 32) periodOffset = 105;
                            }
                            update.CurrentMinute = periodOffset + elapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse match details for event {EventId}, using incident-based minute", eventId);
            }

            // Get cached match info if available
            if (_matchInfoCache.TryGetValue(eventId, out var matchInfo))
            {
                update.HomeTeam = matchInfo.HomeTeam;
                update.AwayTeam = matchInfo.AwayTeam;
                // Chỉ dùng cached status nếu chưa set finished
                if (update.Status != "finished")
                    update.Status = matchInfo.Status;
            }

            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", update, stoppingToken);

            _logger.LogInformation("Broadcasted update for match {EventId} - Score: {Home}:{Away} - Status: {Status}", 
                eventId, update.HomeScore, update.AwayScore, update.Status);

            // Persist score to DB on every poll (live score) and mark FT when finished
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<VNFootballLeagues.Repositories.Models.VNFootballLeaguesDBContext>();
                var dbMatch = await context.Matches.FirstOrDefaultAsync(m => m.ApiFixtureId == eventId, stoppingToken);
                if (dbMatch != null)
                {
                    dbMatch.HomeGoals = update.HomeScore;
                    dbMatch.AwayGoals = update.AwayScore;
                    if (update.Status == "finished")
                        dbMatch.Status = "FT";
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist score for match {EventId}", eventId);
            }
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
