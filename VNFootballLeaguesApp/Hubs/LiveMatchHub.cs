using Microsoft.AspNetCore.SignalR;

namespace VNFootballLeaguesApp.Hubs;

/// <summary>
/// SignalR Hub for broadcasting live match updates to connected clients
/// </summary>
public class LiveMatchHub : Hub
{
    private readonly ILogger<LiveMatchHub> _logger;

    public LiveMatchHub(ILogger<LiveMatchHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to subscribe to specific match updates
    /// </summary>
    /// <param name="eventId">The match event ID to subscribe to</param>
    public async Task SubscribeToMatch(int eventId)
    {
        string groupName = $"match_{eventId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to match {EventId}", Context.ConnectionId, eventId);
    }

    /// <summary>
    /// Allows clients to unsubscribe from specific match updates
    /// </summary>
    /// <param name="eventId">The match event ID to unsubscribe from</param>
    public async Task UnsubscribeFromMatch(int eventId)
    {
        string groupName = $"match_{eventId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from match {EventId}", Context.ConnectionId, eventId);
    }
}
