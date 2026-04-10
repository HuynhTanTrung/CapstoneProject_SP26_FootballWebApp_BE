#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VNFootballLeagues.Repositories.Models;

public partial class PredictionContest
{
    [Key]
    public int ContestId { get; set; }

    /// <summary>TOP4 | POTM | TOP_SCORER | POTS | CHAMPION</summary>
    public string ContestType { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>Thời điểm đóng dự đoán (UTC). Sau thời điểm này không cho submit.</summary>
    public DateTime ClosesAt { get; set; }

    /// <summary>Thời điểm công bố kết quả (UTC). Admin set kết quả sau thời điểm này.</summary>
    public DateTime? ResultAt { get; set; }

    /// <summary>Điểm thưởng khi đoán đúng hoàn toàn.</summary>
    public int PointsExact { get; set; }

    /// <summary>Điểm thưởng khi đoán đúng một phần (dùng cho TOP4: đúng đội sai vị trí).</summary>
    public int PointsPartial { get; set; }

    /// <summary>OPEN | CLOSED | SETTLED</summary>
    public string Status { get; set; } = "OPEN";

    /// <summary>LeagueId liên quan (để lọc đội/cầu thủ).</summary>
    public int? LeagueId { get; set; }

    /// <summary>SeasonId liên quan.</summary>
    public int? SeasonId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual League League { get; set; }
    public virtual Season Season { get; set; }
    public virtual ICollection<ContestEntry> Entries { get; set; } = new List<ContestEntry>();
    public virtual ICollection<ContestResult> Results { get; set; } = new List<ContestResult>();
}
