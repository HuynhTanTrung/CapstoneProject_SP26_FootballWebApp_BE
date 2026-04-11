using VNFootballLeagues.Services.Models.Predictions;

namespace VNFootballLeagues.Services.IServices;

public interface IContestService
{
    // ── Public ──────────────────────────────────────────────────────────────
    Task<List<ContestDto>> GetOpenContestsAsync(Guid? userId, CancellationToken ct = default);
    Task<ContestDto?> GetContestAsync(int contestId, Guid? userId, CancellationToken ct = default);

    // ── User ─────────────────────────────────────────────────────────────────
    Task<(bool Success, string Message)> SubmitEntryAsync(Guid userId, SubmitContestEntryRequest request, CancellationToken ct = default);
    Task<List<ContestDto>> GetSettledContestsForUserAsync(Guid userId, CancellationToken ct = default);

    // ── Admin ────────────────────────────────────────────────────────────────
    Task<ContestDto> CreateContestAsync(CreateContestRequest request, CancellationToken ct = default);
    Task<(bool Success, string Message, int Settled)> SettleContestAsync(SettleContestRequest request, CancellationToken ct = default);
    Task<List<ContestDto>> GetAllContestsAsync(CancellationToken ct = default);
    Task<object> GetContestEntriesAsync(int contestId, CancellationToken ct = default);

    // ── Teams/Players for picker ─────────────────────────────────────────────
    Task<List<TeamPickerDto>> GetTeamsForPickerAsync(int? leagueId, int? seasonId, CancellationToken ct = default);
    Task<List<PlayerPickerDto>> GetPlayersForPickerAsync(int teamId, CancellationToken ct = default);
}

public class TeamPickerDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int? ApiTeamId { get; set; }
}

public class PlayerPickerDto
{
    public int PlayerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public int? ApiPlayerId { get; set; }
}
