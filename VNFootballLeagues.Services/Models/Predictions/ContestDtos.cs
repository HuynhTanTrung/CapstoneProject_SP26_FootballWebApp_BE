namespace VNFootballLeagues.Services.Models.Predictions;

// ── Contest (admin view + public view) ──────────────────────────────────────

public class ContestDto
{
    public int ContestId { get; set; }
    public string ContestType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ClosesAt { get; set; }
    public DateTime? ResultAt { get; set; }
    public int PointsExact { get; set; }
    public int PointsPartial { get; set; }
    public string Status { get; set; } = "OPEN";
    public int? LeagueId { get; set; }
    public int? SeasonId { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>True nếu user hiện tại đã submit dự đoán.</summary>
    public bool HasEntered { get; set; }
    /// <summary>Kết quả chính thức (sau khi settled).</summary>
    public List<ContestResultDto>? Results { get; set; }
    /// <summary>Dự đoán của user hiện tại (nếu đã đăng nhập).</summary>
    public List<ContestEntryDto>? MyEntries { get; set; }
}

// ── Create / Update contest (admin) ─────────────────────────────────────────

public class CreateContestRequest
{
    public string ContestType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ClosesAt { get; set; }
    public int PointsExact { get; set; }
    public int PointsPartial { get; set; }
    public int? LeagueId { get; set; }
    public int? SeasonId { get; set; }
}

// ── Submit entry (user) ──────────────────────────────────────────────────────

public class SubmitContestEntryRequest
{
    public int ContestId { get; set; }
    /// <summary>
    /// Danh sách lựa chọn theo thứ tự rank.
    /// TOP4: 4 phần tử với TeamId.
    /// POTM/TOP_SCORER/POTS: 1 phần tử với PlayerId.
    /// CHAMPION: 1 phần tử với TeamId.
    /// </summary>
    public List<ContestPickDto> Picks { get; set; } = new();
}

public class ContestPickDto
{
    public int Rank { get; set; } = 1;
    public int? TeamId { get; set; }
    public int? PlayerId { get; set; }
}

// ── Entry DTO ────────────────────────────────────────────────────────────────

public class ContestEntryDto
{
    public int EntryId { get; set; }
    public int Rank { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public int? ApiTeamId { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public int? Points { get; set; }
    public int? IsCorrect { get; set; }
}

// ── Result DTO ───────────────────────────────────────────────────────────────

public class ContestResultDto
{
    public int Rank { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public int? ApiTeamId { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
}

// ── Settle (admin) ───────────────────────────────────────────────────────────

public class SettleContestRequest
{
    public int ContestId { get; set; }
    public List<ContestPickDto> Results { get; set; } = new();
}
