using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Predictions;

namespace VNFootballLeagues.Services.Services;

public class ContestService : IContestService
{
    private readonly VNFootballLeaguesDBContext _db;

    public ContestService(VNFootballLeaguesDBContext db) => _db = db;

    // ── Public ───────────────────────────────────────────────────────────────

    public async Task<List<ContestDto>> GetOpenContestsAsync(Guid? userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var contests = await _db.PredictionContests
            .Where(c => c.Status == "OPEN" || c.Status == "CLOSED")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return await MapContestListAsync(contests, userId, ct);
    }

    public async Task<ContestDto?> GetContestAsync(int contestId, Guid? userId, CancellationToken ct = default)
    {
        var contest = await _db.PredictionContests.FindAsync(new object[] { contestId }, ct);
        if (contest == null) return null;
        return await MapContestAsync(contest, userId, ct);
    }

    // ── User ─────────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Message)> SubmitEntryAsync(Guid userId, SubmitContestEntryRequest request, CancellationToken ct = default)
    {
        var contest = await _db.PredictionContests.FindAsync(new object[] { request.ContestId }, ct);
        if (contest == null) return (false, "Không tìm thấy contest.");
        if (contest.Status != "OPEN") return (false, "Contest đã đóng.");
        if (DateTime.UtcNow > contest.ClosesAt) return (false, "Đã qua thời hạn dự đoán.");

        // Validate picks
        var picks = request.Picks;
        if (contest.ContestType == "TOP4")
        {
            if (picks.Count != 4) return (false, "Cần chọn đúng 4 đội.");
            if (picks.Any(p => p.TeamId == null)) return (false, "Vui lòng chọn đội cho tất cả vị trí.");
            if (picks.Select(p => p.TeamId).Distinct().Count() != 4) return (false, "Không được chọn trùng đội.");
        }
        else if (contest.ContestType == "CHAMPION")
        {
            if (picks.Count != 1 || picks[0].TeamId == null) return (false, "Cần chọn 1 đội vô địch.");
        }
        else // POTM, TOP_SCORER, POTS
        {
            if (picks.Count != 1 || picks[0].PlayerId == null) return (false, "Cần chọn 1 cầu thủ.");
        }

        // Remove existing entries for this user+contest
        var existing = await _db.ContestEntries
            .Where(e => e.ContestId == request.ContestId && e.UserId == userId)
            .ToListAsync(ct);
        bool isFirstEntry = !existing.Any();
        if (existing.Any()) _db.ContestEntries.RemoveRange(existing);

        // Add new entries
        foreach (var pick in picks)
        {
            _db.ContestEntries.Add(new ContestEntry
            {
                ContestId = request.ContestId,
                UserId = userId,
                Rank = pick.Rank,
                TeamId = pick.TeamId,
                PlayerId = pick.PlayerId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Bump TotalPredictions only on first submission (not re-entry)
        if (isFirstEntry)
        {
            var stats = await _db.UserPredictionStats.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (stats == null)
            {
                _db.UserPredictionStats.Add(new UserPredictionStats
                {
                    UserId = userId,
                    TotalPredictions = 1,
                    Points = 0,
                    CorrectPredictions = 0,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                stats.TotalPredictions = (stats.TotalPredictions ?? 0) + 1;
                stats.LastUpdated = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (true, "Đã lưu dự đoán.");
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<ContestDto> CreateContestAsync(CreateContestRequest request, CancellationToken ct = default)
    {
        var contest = new PredictionContest
        {
            ContestType = request.ContestType,
            Title = request.Title,
            Description = request.Description,
            ClosesAt = DateTime.SpecifyKind(request.ClosesAt, DateTimeKind.Utc),
            PointsExact = request.PointsExact,
            PointsPartial = request.PointsPartial,
            Status = "OPEN",
            LeagueId = request.LeagueId,
            SeasonId = request.SeasonId,
            CreatedAt = DateTime.UtcNow
        };
        _db.PredictionContests.Add(contest);
        await _db.SaveChangesAsync(ct);
        return await MapContestAsync(contest, null, ct);
    }

    public async Task<(bool Success, string Message, int Settled)> SettleContestAsync(SettleContestRequest request, CancellationToken ct = default)
    {
        var contest = await _db.PredictionContests.FindAsync(new object[] { request.ContestId }, ct);
        if (contest == null) return (false, "Không tìm thấy contest.", 0);
        if (contest.Status == "SETTLED") return (false, "Contest đã được chấm.", 0);

        // Save official results
        var oldResults = await _db.ContestResults.Where(r => r.ContestId == request.ContestId).ToListAsync(ct);
        _db.ContestResults.RemoveRange(oldResults);
        foreach (var r in request.Results)
        {
            _db.ContestResults.Add(new ContestResult
            {
                ContestId = request.ContestId,
                Rank = r.Rank,
                TeamId = r.TeamId,
                PlayerId = r.PlayerId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Grade all entries
        var entries = await _db.ContestEntries
            .Where(e => e.ContestId == request.ContestId)
            .ToListAsync(ct);

        var resultTeamIds = request.Results.Select(r => r.TeamId).ToHashSet();
        var resultPlayerIds = request.Results.Select(r => r.PlayerId).ToHashSet();
        var resultByRank = request.Results.ToDictionary(r => r.Rank, r => r);

        // Group entries by user
        var byUser = entries.GroupBy(e => e.UserId);
        int settled = 0;

        foreach (var userGroup in byUser)
        {
            int totalPoints = 0;
            var userEntries = userGroup.ToList();

            if (contest.ContestType == "TOP4")
            {
                // TOP4: grade as a whole unit
                // Exact = all 4 teams correct AND all 4 positions correct
                // Partial = all 4 teams are in the official top4 but at least 1 wrong position
                // 0 = any team not in official top4

                bool allTeamsPresent = userEntries.All(e =>
                    e.TeamId.HasValue && resultTeamIds.Contains(e.TeamId));

                bool allPositionsCorrect = allTeamsPresent && userEntries.All(e =>
                    resultByRank.TryGetValue(e.Rank, out var res) && res.TeamId == e.TeamId);

                int groupPts = 0;
                int groupIsCorrect = 0;

                if (allPositionsCorrect)
                {
                    groupPts = contest.PointsExact;
                    groupIsCorrect = 2; // exact
                }
                else if (allTeamsPresent)
                {
                    groupPts = contest.PointsPartial;
                    groupIsCorrect = 1; // right teams, wrong positions
                }
                // else 0

                // Apply result: only rank 1 entry gets the points, others get 0 (to avoid summing)
                foreach (var entry in userEntries)
                {
                    entry.IsCorrect = groupIsCorrect;
                    entry.Points = entry.Rank == 1 ? groupPts : 0;
                }
                totalPoints = groupPts;
            }
            else
            {
                foreach (var entry in userEntries)
                {
                    int pts = 0;
                    int isCorrect = 0;

                    if (contest.ContestType == "CHAMPION")
                    {
                        bool correct = entry.TeamId.HasValue && resultTeamIds.Contains(entry.TeamId);
                        if (correct) { pts = contest.PointsExact; isCorrect = 2; }
                    }
                    else // POTM, TOP_SCORER, POTS
                    {
                        bool correct = entry.PlayerId.HasValue && resultPlayerIds.Contains(entry.PlayerId);
                        if (correct) { pts = contest.PointsExact; isCorrect = 2; }
                    }

                    entry.Points = pts;
                    entry.IsCorrect = isCorrect;
                    totalPoints += pts;
                }
            }

            // Update UserPredictionStats
            {
                var stats = await _db.UserPredictionStats.FindAsync(new object[] { userGroup.Key }, ct);
                // For TOP4: count as 1 prediction (the whole group), not 4
                int contestEntryCount = contest.ContestType == "TOP4" ? 1 : userEntries.Count;
                int correctCount = contest.ContestType == "TOP4"
                    ? (userEntries.First().IsCorrect > 0 ? 1 : 0)
                    : userEntries.Count(e => (e.IsCorrect ?? 0) > 0);

                if (stats == null)
                {
                    _db.UserPredictionStats.Add(new UserPredictionStats
                    {
                        UserId = userGroup.Key,
                        Points = totalPoints,
                        TotalPredictions = contestEntryCount,
                        CorrectPredictions = correctCount,
                        LastUpdated = DateTime.UtcNow
                    });
                }
                else
                {
                    stats.Points = (stats.Points ?? 0) + totalPoints;
                    stats.TotalPredictions = (stats.TotalPredictions ?? 0) + contestEntryCount;
                    stats.CorrectPredictions = (stats.CorrectPredictions ?? 0) + correctCount;
                    stats.LastUpdated = DateTime.UtcNow;
                }
            }
            settled++;
        }

        contest.Status = "SETTLED";
        contest.ResultAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, $"Đã chấm {settled} người dùng.", settled);
    }

    public async Task<List<ContestDto>> GetAllContestsAsync(CancellationToken ct = default)
    {
        var contests = await _db.PredictionContests
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return await MapContestListAsync(contests, null, ct);
    }

    public async Task<List<ContestDto>> GetSettledContestsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Get contest IDs where user has entries
        var enteredIds = await _db.ContestEntries
            .Where(e => e.UserId == userId)
            .Select(e => e.ContestId)
            .Distinct().ToListAsync(ct);

        var contests = await _db.PredictionContests
            .Where(c => c.Status == "SETTLED" && enteredIds.Contains(c.ContestId))
            .OrderByDescending(c => c.ResultAt)
            .ToListAsync(ct);

        return await MapContestListAsync(contests, userId, ct);
    }

    public async Task<object> GetContestEntriesAsync(int contestId, CancellationToken ct = default)
    {
        var contest = await _db.PredictionContests.FindAsync(new object[] { contestId }, ct);
        if (contest == null) return new { };

        var entries = await _db.ContestEntries
            .Include(e => e.User)
            .Include(e => e.Team)
            .Include(e => e.Player)
            .Where(e => e.ContestId == contestId)
            .OrderByDescending(e => e.Points)
            .ToListAsync(ct);

        var results = await _db.ContestResults
            .Include(r => r.Team)
            .Include(r => r.Player)
            .Where(r => r.ContestId == contestId)
            .OrderBy(r => r.Rank)
            .ToListAsync(ct);

        // Group by user for TOP4
        var byUser = entries.GroupBy(e => e.UserId).Select(g => new {
            userId = g.Key,
            username = g.First().User?.FullName ?? g.First().User?.Username ?? "?",
            totalPoints = g.Sum(e => e.Points ?? 0),
            correctCount = g.Count(e => (e.IsCorrect ?? 0) == 2),
            partialCount = g.Count(e => (e.IsCorrect ?? 0) == 1),
            picks = g.OrderBy(e => e.Rank).Select(e => new {
                e.Rank,
                teamName = e.Team?.TeamName,
                apiTeamId = e.Team?.ApiTeamId,
                playerName = e.Player?.FullName,
                e.Points,
                e.IsCorrect
            }).ToList()
        }).OrderByDescending(u => u.totalPoints).ToList();

        return new {
            contestId,
            title = contest.Title,
            contestType = contest.ContestType,
            officialResults = results.Select(r => new {
                r.Rank,
                teamName = r.Team?.TeamName,
                apiTeamId = r.Team?.ApiTeamId,
                playerName = r.Player?.FullName
            }),
            totalEntrants = byUser.Count,
            entries = byUser
        };
    }

    // ── Pickers ──────────────────────────────────────────────────────────────

    public async Task<List<TeamPickerDto>> GetTeamsForPickerAsync(int? leagueId, int? seasonId, CancellationToken ct = default)
    {
        var query = _db.Teams.AsQueryable();
        if (leagueId.HasValue)
            query = query.Where(t => t.LeagueId == leagueId);
        return await query
            .OrderBy(t => t.TeamName)
            .Select(t => new TeamPickerDto { TeamId = t.TeamId, TeamName = t.TeamName, ApiTeamId = t.ApiTeamId })
            .ToListAsync(ct);
    }

    public async Task<List<PlayerPickerDto>> GetPlayersForPickerAsync(int teamId, CancellationToken ct = default)
    {
        return await _db.Players
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.FullName)
            .Select(p => new PlayerPickerDto
            {
                PlayerId = p.PlayerId,
                FullName = p.FullName,
                Position = p.Position,
                ApiPlayerId = p.ApiPlayerId
            })
            .ToListAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<ContestDto>> MapContestListAsync(List<PredictionContest> contests, Guid? userId, CancellationToken ct)
    {
        var result = new List<ContestDto>();
        foreach (var c in contests)
            result.Add(await MapContestAsync(c, userId, ct));
        return result;
    }

    private async Task<ContestDto> MapContestAsync(PredictionContest c, Guid? userId, CancellationToken ct)
    {
        // Auto-close if past deadline
        if (c.Status == "OPEN" && DateTime.UtcNow > c.ClosesAt)
        {
            c.Status = "CLOSED";
            await _db.SaveChangesAsync(ct);
        }

        var dto = new ContestDto
        {
            ContestId = c.ContestId,
            ContestType = c.ContestType,
            Title = c.Title,
            Description = c.Description,
            ClosesAt = c.ClosesAt,
            ResultAt = c.ResultAt,
            PointsExact = c.PointsExact,
            PointsPartial = c.PointsPartial,
            Status = c.Status,
            LeagueId = c.LeagueId,
            SeasonId = c.SeasonId,
            CreatedAt = c.CreatedAt
        };

        // Results
        var results = await _db.ContestResults
            .Where(r => r.ContestId == c.ContestId)
            .Join(_db.Teams, r => r.TeamId, t => t.TeamId, (r, t) => new { r, TeamName = t.TeamName, ApiTeamId = t.ApiTeamId })
            .Select(x => new ContestResultDto { Rank = x.r.Rank, TeamId = x.r.TeamId, TeamName = x.TeamName, ApiTeamId = x.ApiTeamId })
            .ToListAsync(ct);

        var playerResults = await _db.ContestResults
            .Where(r => r.ContestId == c.ContestId && r.PlayerId != null)
            .Join(_db.Players, r => r.PlayerId, p => p.PlayerId, (r, p) => new { r, PlayerName = p.FullName })
            .Select(x => new ContestResultDto { Rank = x.r.Rank, PlayerId = x.r.PlayerId, PlayerName = x.PlayerName })
            .ToListAsync(ct);

        dto.Results = results.Any() ? results : (playerResults.Any() ? playerResults : null);

        // My entries
        if (userId.HasValue)
        {
            var myEntries = await _db.ContestEntries
                .Where(e => e.ContestId == c.ContestId && e.UserId == userId.Value)
                .ToListAsync(ct);

            if (myEntries.Any())
            {
                dto.HasEntered = true;
                dto.MyEntries = new List<ContestEntryDto>();
                foreach (var e in myEntries)
                {
                    string? teamName = e.TeamId.HasValue
                        ? (await _db.Teams.FindAsync(new object[] { e.TeamId.Value }, ct))?.TeamName : null;
                    int? apiTeamId = e.TeamId.HasValue
                        ? (await _db.Teams.FindAsync(new object[] { e.TeamId.Value }, ct))?.ApiTeamId : null;
                    string? playerName = e.PlayerId.HasValue
                        ? (await _db.Players.FindAsync(new object[] { e.PlayerId.Value }, ct))?.FullName : null;
                    dto.MyEntries.Add(new ContestEntryDto
                    {
                        EntryId = e.EntryId,
                        Rank = e.Rank,
                        TeamId = e.TeamId,
                        TeamName = teamName,
                        ApiTeamId = apiTeamId,
                        PlayerId = e.PlayerId,
                        PlayerName = playerName,
                        Points = e.Points,
                        IsCorrect = e.IsCorrect
                    });
                }
            }
        }

        return dto;
    }
}
