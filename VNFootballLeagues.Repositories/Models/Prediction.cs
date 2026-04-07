#nullable disable
using System;

namespace VNFootballLeagues.Repositories.Models;

/// <summary>
/// Dự đoán tỉ số. Sau khi trận kết thúc: Points = 3 (đúng tỉ số), 1 (đúng thắng/thua/hòa), 0 (sai).
/// IsCorrect: 0 = sai, 1 = đúng kết quả, 2 = đúng tỉ số.
/// </summary>
public partial class Prediction
{
    public int PredictionId { get; set; }

    public int? PredictedHomeGoals { get; set; }

    public int? PredictedAwayGoals { get; set; }

    public int? IsCorrect { get; set; }

    public int? Points { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? UserId { get; set; }

    public int? MatchId { get; set; }

    public virtual User User { get; set; }

    public virtual Match Match { get; set; }
}
