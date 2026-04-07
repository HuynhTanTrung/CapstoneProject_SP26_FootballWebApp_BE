#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VNFootballLeagues.Repositories.Models;

public partial class ContestEntry
{
    [Key]
    public int EntryId { get; set; }

    public int ContestId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Thứ hạng dự đoán (1-4 cho TOP4, 1 cho các loại khác).</summary>
    public int Rank { get; set; } = 1;

    /// <summary>TeamId được chọn (dùng cho TOP4, CHAMPION).</summary>
    public int? TeamId { get; set; }

    /// <summary>PlayerId được chọn (dùng cho POTM, TOP_SCORER, POTS).</summary>
    public int? PlayerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Điểm nhận được sau khi chấm. Null = chưa chấm.</summary>
    public int? Points { get; set; }

    /// <summary>0 = sai, 1 = đúng một phần, 2 = đúng hoàn toàn. Null = chưa chấm.</summary>
    public int? IsCorrect { get; set; }

    public virtual PredictionContest Contest { get; set; }
    public virtual User User { get; set; }
    public virtual Team Team { get; set; }
    public virtual Player Player { get; set; }
}
