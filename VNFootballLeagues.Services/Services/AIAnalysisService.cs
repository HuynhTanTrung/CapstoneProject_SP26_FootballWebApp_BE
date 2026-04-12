using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services;

public class AIAnalysisService : IAIAnalysisService
{
    private const string SYSTEM_PROMPT_PLAYER = """
        Bạn là chuyên gia phân tích bóng đá của hệ thống VN Player Rating, viết bằng tiếng Việt tự nhiên.
        Nhiệm vụ: dựa trên dữ liệu thống kê JSON và CÔNG THỨC CHẤM ĐIỂM bên dưới,
        giải thích tại sao cầu thủ nhận rating đó trong trận này.

        === CÔNG THỨC CHẤM ĐIỂM (VN Player Rating v3.3) ===
        Điểm nền: 6.0 | Thang: 1.0–10.0
        Vị trí: F=Tiền đạo, M=Tiền vệ, D=Hậu vệ, G=Thủ môn

        RawRating = 6.0 + AttackScore(cap+2.5) + PassingScore(cap+0.8) + DefenseScore(cap+1.2)
                    + DuelScore(cap+0.4) + DribblingScore(cap+0.3) + TeamResultBonus(cap+0.3)
                    + GKScore(cap+3.0, chỉ G) - NegativeScore(cap-2.5) - CardPenalty

        ** AttackScore (cap +2.5) **
        Goals: F +0.5, M +0.7, D +0.9, G +1.8 mỗi bàn
        Assists: F +0.4, M +0.5, D +0.4, G +0.8
        ShotsOnTarget (không tính bàn thắng): F +0.10 (cap+0.20), M/D +0.08 (cap+0.16). Nếu Goals=0 thì ×0.7
        PassesKey: F +0.08 (cap+0.24), M +0.10 (cap+0.30), D +0.08 (cap+0.24)
        xG: xGScore = Clamp((Goals-ExpectedGoals)×0.2, -0.3, +0.3)
        xA: xAScore = Clamp((Assists-ExpectedAssists)×0.15, -0.15, +0.15)
        PenaltiesWon: F +0.3, M +0.25, D +0.15
        PenaltiesMissed: -0.6
        AccurateCrosses: F +0.06, M +0.08, D +0.06 (cap+0.2)

        ** PassingScore (cap +0.8) **
        PassAccuracyRate = PassesAccuracy / Passes
        VolumeMultiplier = min(1.0, Passes/30)
        KeyPassMultiplier = (PassesKey==0) ? 0.8 : 1.0
        DiminishingMultiplier = (Passes>60) ? 0.9 : 1.0
        F: ≥80% → +0.2, 65-79% → 0, <65% → -0.15
        M: ≥88% → +0.8, 82-87% → +0.5, 75-81% → +0.2, 65-74% → 0, <65% → -0.5
        D: ≥88% → +0.5, 78-87% → +0.2, 65-77% → 0, <65% → -0.4
        G: ≥75% → +0.2, 55-74% → 0, <55% → -0.3
        AccurateLongBalls: M +0.05, D +0.06, G +0.08 (cap+0.2)

        ** DefenseScore (cap +1.2) **
        TacklesWon: F +0.06(cap+0.18), M +0.10(cap+0.40), D +0.16(cap+0.64)
        Interceptions: F +0.05(cap+0.15), M +0.08(cap+0.32), D +0.13(cap+0.52), G +0.08(cap+0.16)
        Clearances: D +0.07(cap+0.28), G +0.05(cap+0.15)
        Blocks: M +0.10(cap+0.20), D +0.18(cap+0.36)
        BallRecoveries: F +0.03, M +0.04, D +0.05 (cap+0.2)
        DribbledPast = Tackles-TacklesWon, bỏ lần đầu: M -0.06, D -0.08 (cap-0.4)

        ** DuelScore (cap +0.4) **
        GroundDuels: ≥65% → +0.2, 45-64% → 0, <45% → -0.1
        AerialDuels ≥60%: F +0.12, M +0.08, D +0.15, G +0.10 | <40%: F -0.06, M -0.04, D -0.08, G -0.05

        ** DribblingScore (cap +0.3) **
        Rate = DribblesSuccess/DribblesAttempted, VolumeMultiplier = (Attempted<3)?0.5:1.0
        ≥70% → +0.2, 50-69% → +0.1, <40% → -0.08
        Bonus DribblesSuccess≥3: F +0.08, M +0.06

        ** TeamResultBonus ** Thắng +0.3, Hòa 0, Thua -0.2 (nếu RawRating<6.0 thì ×0.5)

        ** GKScore (cap +3.0, chỉ vị trí G) **
        SavesOutsideBox×0.35 + SavesInsideBox×0.50 (cap+2.0)
        Sạch lưới ≥60ph: Saves≥2 → +1.0, Saves<2 → +0.6 | 30-59ph → +0.5
        GoalsConceded: -0.4/bàn (cap-1.6)
        PenaltiesSaved: +1.2/lần
        RunsOutSuccessful: +0.15 (cap+0.30), HighClaims: +0.10 (cap+0.20)

        ** NegativeScore (cap -2.5) **
        FoulsCommitted -0.08 (cap-0.48), PenaltiesCommitted -0.8 (cap-0.8)
        PossessionLost -0.05 (cap-0.75), Offsides -0.06 (cap-0.18)
        Dispossessed -0.05 (cap-0.25), UnsuccessfulTouch -0.03 (cap-0.30)

        ** CardPenalty **
        Thẻ vàng: F -0.4, M/D -0.5, G -0.8
        Thẻ đỏ trực tiếp: F -1.5, M/D -2.0, G -3.0
        2 thẻ vàng: F -1.9, M/D -2.5, G -3.8

        ** Chuẩn hóa thời gian **
        ≥60ph: giữ nguyên
        <60ph: FinalRating = RawRating × (0.6 + 0.4×MinutesPlayed/90) × (1+ImpactBonus)
        ImpactBonus (≤30ph): Ghi bàn QĐ ≤20ph +0.4, Kiến tạo QĐ ≤20ph +0.2
        Nếu ImpactBonus>0: floor=6.2

        ** Luân lưu penalty **
        Sút thành công +0.3, Sút hỏng -0.25, GK cản +0.6, GK thủng -0.1

        === KẾT THÚC CÔNG THỨC ===

        Yêu cầu:
        - Phân tích dựa trên công thức trên. Nêu rõ từng thành phần điểm (AttackScore, PassingScore, DefenseScore, v.v.)
          đã đóng góp bao nhiêu vào rating dựa trên số liệu thực trong JSON.
        - Chỉ dùng số liệu trong JSON, không bịa. Nếu thiếu dữ liệu cho 1 thành phần thì ghi "không có dữ liệu".
        - Bố cục markdown:
          ## Tổng quan  (1-2 câu nêu rating + vị trí + đội)
          ## Phân tích chi tiết
          ### Điểm tấn công (AttackScore)
          ### Điểm chuyền bóng (PassingScore)
          ### Điểm phòng thủ (DefenseScore)
          ### Điểm tranh chấp & rê bóng (DuelScore + DribblingScore)
          ### Điểm tiêu cực & thẻ phạt (NegativeScore + CardPenalty)
          ### Điểm thủ môn (GKScore) — chỉ nếu vị trí G
          ### Bonus kết quả đội (TeamResultBonus)
          ## Kết luận  (1-2 câu tổng kết)
        - Mỗi mục nêu số liệu cụ thể + điểm đóng góp ước tính theo công thức.
        - Bỏ qua mục nào không có dữ liệu hoặc đóng góp = 0.
        - Độ dài: 200-400 từ. Không lặp lại JSON thô.
        """;

    private const string SYSTEM_PROMPT_MATCH = """
        Bạn là bình luận viên bóng đá, viết bằng tiếng Việt.
        Nhiệm vụ: thuật lại diễn biến và phân tích trận đấu dựa trên JSON.
        Bố cục markdown:
          ## Tổng quan trận đấu  (đội, tỉ số, sân, trọng tài)
          ## Diễn biến chính  (theo phút, mỗi sự kiện 1 dòng)
          ## Thống kê nổi bật  (so sánh 2 đội)
          ## Nhận định  (2-3 câu kết)
        - Chỉ dùng dữ liệu JSON, không bịa.
        - Nếu mảng events trống: bỏ "Diễn biến chính", ghi "Không có dữ liệu sự kiện".
        - Độ dài: 250-450 từ.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly VNFootballLeaguesDBContext _db;
    private readonly IGeminiService _gemini;
    private readonly ISofascoreHybridService _sofascore;
    private readonly ILogger<AIAnalysisService> _logger;

    public AIAnalysisService(
        VNFootballLeaguesDBContext db,
        IGeminiService gemini,
        ISofascoreHybridService sofascore,
        ILogger<AIAnalysisService> logger)
    {
        _db = db;
        _gemini = gemini;
        _sofascore = sofascore;
        _logger = logger;
    }

    public async Task<AIAnalysisResponse> AnalyzePlayerRatingAsync(int matchId, int playerId, CancellationToken ct = default)
    {
        if (matchId <= 0)
        {
            throw new ArgumentException("matchId không hợp lệ");
        }

        if (playerId <= 0)
        {
            throw new ArgumentException("playerId không hợp lệ");
        }

        ct.ThrowIfCancellationRequested();

        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
        {
            throw new KeyNotFoundException("Không tìm thấy trận đấu");
        }

        var stat = await _db.PlayerMatchStatistics
            .AsNoTracking()
            .Include(s => s.Player)
            .Include(s => s.Team)
            .FirstOrDefaultAsync(s => s.MatchId == matchId && s.PlayerId == playerId, ct);

        if (stat == null)
        {
            throw new KeyNotFoundException("Cầu thủ không thi đấu trận này");
        }

        if (stat.Rating == null && stat.SofascoreRating == null)
        {
            throw new ArgumentException("Chưa có rating cho cầu thủ");
        }

        var payload = BuildPlayerRatingPayload(match, stat.Player, stat);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        ct.ThrowIfCancellationRequested();
        var result = await _gemini.ChatWithSystemContextAsync(
            SYSTEM_PROMPT_PLAYER,
            [("user", $"Dữ liệu cầu thủ:\n```json\n{json}\n```")]);

        var success = !result.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase);
        return new AIAnalysisResponse(success, "player-rating", result, payload, null);
    }

    public async Task<AIAnalysisResponse> AnalyzeMatchAsync(int matchId, CancellationToken ct = default)
    {
        if (matchId <= 0)
        {
            throw new ArgumentException("matchId không hợp lệ");
        }

        ct.ThrowIfCancellationRequested();

        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
        {
            throw new KeyNotFoundException("Không tìm thấy trận đấu");
        }

        var statistics = await _db.MatchStatistics
            .AsNoTracking()
            .Include(s => s.Team)
            .Where(s => s.MatchId == matchId)
            .ToListAsync(ct);

        var warning = await EnsureMatchEventsAsync(match, ct);

        var events = await _db.MatchEvents
            .AsNoTracking()
            .Include(e => e.Player)
            .Include(e => e.Team)
            .Where(e => e.MatchId == matchId)
            .ToListAsync(ct);

        var orderedEvents = events
            .OrderBy(e => GetPeriodSortOrder(e.Period))
            .ThenBy(e => e.EventTime ?? int.MaxValue)
            .ThenBy(e => e.ExtraTime ?? 0)
            .Take(80)
            .ToList();

        var assistPlayerIds = orderedEvents
            .Where(e => e.AssistPlayerId.HasValue)
            .Select(e => e.AssistPlayerId!.Value)
            .Distinct()
            .ToList();

        var assistPlayers = assistPlayerIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Players
                .AsNoTracking()
                .Where(p => assistPlayerIds.Contains(p.PlayerId))
                .ToDictionaryAsync(p => p.PlayerId, p => p.FullName ?? string.Empty, ct);

        if (statistics.Count == 0 && orderedEvents.Count == 0)
        {
            warning = CombineWarnings(warning, "Dữ liệu hạn chế");
        }

        var payload = BuildMatchPayload(match, statistics, orderedEvents, assistPlayers);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        ct.ThrowIfCancellationRequested();
        var result = await _gemini.ChatWithSystemContextAsync(
            SYSTEM_PROMPT_MATCH,
            [("user", $"Dữ liệu trận đấu:\n```json\n{json}\n```")]);

        var success = !result.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase);
        return new AIAnalysisResponse(success, "match", result, payload, warning);
    }

    private object BuildPlayerRatingPayload(Match match, Player? player, PlayerMatchStatistic stat)
    {
        var matchPayload = CompactObject(new
        {
            match.MatchId,
            match.MatchDate,
            match.Round,
            match.Status,
            match.Venue,
            Referee = match.RefereeName,
            HomeTeamName = match.HomeTeam?.TeamName,
            AwayTeamName = match.AwayTeam?.TeamName,
            match.HomeGoals,
            match.AwayGoals
        });

        var playerPayload = CompactObject(new
        {
            PlayerId = stat.PlayerId,
            FullName = player?.FullName,
            TeamId = stat.TeamId,
            TeamName = stat.Team?.TeamName,
            Position = player?.Position,
            Rating = stat.Rating ?? stat.SofascoreRating,
            RawRating = stat.Rating,
            RawSofascoreRating = stat.SofascoreRating,
            stat.Minutes
        });

        var statisticsPayload = CompactObject(new
        {
            stat.Goals,
            stat.Assists,
            stat.Shots,
            stat.ShotsOnTarget,
            stat.Passes,
            stat.PassesAccuracy,
            stat.PassesKey,
            stat.TotalCrosses,
            stat.AccurateCrosses,
            stat.TotalLongBalls,
            stat.AccurateLongBalls,
            stat.PassesOwnHalf,
            stat.AccuratePassesOwnHalf,
            stat.PassesOppositionHalf,
            stat.AccuratePassesOppositionHalf,
            stat.Tackles,
            stat.TacklesWon,
            stat.Interceptions,
            stat.Clearances,
            stat.Blocks,
            stat.DribblesAttempted,
            stat.DribblesSuccess,
            stat.DuelsWon,
            stat.DuelsTotal,
            stat.AerialDuelsWon,
            stat.AerialDuelsLost,
            stat.GroundDuelsWon,
            stat.GroundDuelsLost,
            stat.FoulsCommitted,
            stat.FoulsDrawn,
            stat.Offsides,
            stat.YellowCards,
            stat.RedCards,
            stat.PenaltiesScored,
            stat.PenaltiesMissed,
            stat.PenaltiesWon,
            stat.PenaltiesCommitted,
            stat.ExpectedGoals,
            stat.ExpectedAssists,
            stat.Touches,
            stat.PossessionLost,
            stat.BallRecoveries,
            stat.Dispossessed,
            stat.WasFouled,
            stat.UnsuccessfulTouch,
            stat.Saves,
            stat.SavesInsideBox,
            stat.Punches,
            stat.RunsOut,
            stat.RunsOutSuccessful,
            stat.HighClaims,
            stat.GoalsConceded,
            stat.PenaltiesSaved,
            stat.IsExtraTime,
            stat.GoalsInExtraTime,
            stat.AssistsInExtraTime,
            stat.PenaltyShootoutScored,
            stat.PenaltyShootoutMissed,
            stat.PenaltyShootoutSaved,
            stat.PenaltyShootoutConceded
        });

        return new
        {
            match = matchPayload,
            player = playerPayload,
            statistics = statisticsPayload
        };
    }

    private object BuildMatchPayload(
        Match match,
        IReadOnlyList<MatchStatistic> statistics,
        IReadOnlyList<MatchEvent> events,
        IReadOnlyDictionary<int, string> assistPlayers)
    {
        var matchPayload = CompactObject(new
        {
            match.MatchId,
            match.MatchDate,
            match.Round,
            match.Status,
            match.Venue,
            Referee = match.RefereeName,
            HomeGoals = match.HomeGoals,
            AwayGoals = match.AwayGoals,
            match.HomePenalties,
            match.AwayPenalties
        });

        var homeStats = statistics.FirstOrDefault(s => s.TeamId == match.HomeTeamId);
        var awayStats = statistics.FirstOrDefault(s => s.TeamId == match.AwayTeamId);

        var homeTeamPayload = new
        {
            name = match.HomeTeam?.TeamName ?? string.Empty,
            stats = homeStats == null ? new Dictionary<string, object?>() : BuildMatchStatisticsPayload(homeStats)
        };

        var awayTeamPayload = new
        {
            name = match.AwayTeam?.TeamName ?? string.Empty,
            stats = awayStats == null ? new Dictionary<string, object?>() : BuildMatchStatisticsPayload(awayStats)
        };

        var eventsPayload = events
            .Select(e => new
            {
                minute = e.EventTime,
                e.ExtraTime,
                e.Period,
                type = e.EventType,
                e.Detail,
                teamName = e.Team?.TeamName,
                playerName = e.Player?.FullName,
                assistPlayerName = e.AssistPlayerId.HasValue && assistPlayers.TryGetValue(e.AssistPlayerId.Value, out var assistName)
                    ? assistName
                    : null,
                comments = e.Comments
            })
            .ToList();

        return new
        {
            match = matchPayload,
            homeTeam = homeTeamPayload,
            awayTeam = awayTeamPayload,
            events = eventsPayload
        };
    }

    private Dictionary<string, object?> BuildMatchStatisticsPayload(MatchStatistic statistic) =>
        CompactObject(new
        {
            statistic.Possession,
            statistic.Shots,
            statistic.ShotsOnTarget,
            statistic.Corners,
            statistic.Fouls,
            statistic.YellowCards,
            statistic.RedCards,
            statistic.Offsides,
            statistic.ShotsBlocked,
            statistic.ShotsInsideBox,
            statistic.ShotsOutsideBox,
            statistic.PassesAccuracy,
            statistic.PassesKey,
            statistic.DribblesAttempted,
            statistic.DribblesSuccess,
            statistic.DuelsWon,
            statistic.DuelsTotal,
            statistic.TacklesWon,
            statistic.Saves,
            statistic.Interceptions,
            statistic.Clearances,
            statistic.ExpectedGoals
        });

    private async Task<string?> EnsureMatchEventsAsync(Match match, CancellationToken ct)
    {
        var hasEvents = await _db.MatchEvents
            .AsNoTracking()
            .AnyAsync(e => e.MatchId == match.MatchId, ct);

        if (hasEvents)
        {
            return null;
        }

        if (!match.ApiFixtureId.HasValue)
        {
            return "Không có ApiFixtureId - không thể đồng bộ sự kiện";
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            var syncResult = await _sofascore.SyncMatchEventsAsync(match.ApiFixtureId.Value);

            if (!IsSuccessfulSync(syncResult))
            {
                var syncMessage = TryGetStringProperty(syncResult, "message");
                _logger.LogWarning(
                    "Sync match events returned unsuccessful status for MatchId={MatchId}. Message={Message}",
                    match.MatchId,
                    syncMessage);

                return "Không thể đồng bộ sự kiện từ SofaScore - phân tích dựa trên thống kê có sẵn";
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync match events failed for MatchId={MatchId}", match.MatchId);
            return "Không thể đồng bộ sự kiện từ SofaScore - phân tích dựa trên thống kê có sẵn";
        }
    }

    private static Dictionary<string, object?> CompactObject(object source)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = property.GetValue(source);
            if (!ShouldIncludeValue(value))
            {
                continue;
            }

            result[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = value;
        }

        return result;
    }

    private static bool ShouldIncludeValue(object? value) =>
        value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            bool b => b,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            float number => number != 0,
            double number => number != 0,
            decimal number => number != 0,
            IDictionary<string, object?> dictionary => dictionary.Count > 0,
            _ => true
        };

    private static int GetPeriodSortOrder(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return 99;
        }

        var normalized = period.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1st half" => 1,
            "first half" => 1,
            "2nd half" => 2,
            "second half" => 2,
            "regular" => 2,
            "extra time" => 3,
            "penalties" => 4,
            _ => 99
        };
    }

    private static bool IsSuccessfulSync(object? syncResult)
    {
        if (syncResult == null)
        {
            return false;
        }

        var statusProperty = syncResult.GetType().GetProperty(
            "status",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (statusProperty?.PropertyType == typeof(bool))
        {
            return (bool)(statusProperty.GetValue(syncResult) ?? false);
        }

        return true;
    }

    private static string? TryGetStringProperty(object? source, string propertyName)
    {
        if (source == null)
        {
            return null;
        }

        var property = source.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return property?.GetValue(source) as string;
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var filtered = warnings
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct()
            .ToArray();

        return filtered.Length == 0 ? null : string.Join("; ", filtered);
    }
}
