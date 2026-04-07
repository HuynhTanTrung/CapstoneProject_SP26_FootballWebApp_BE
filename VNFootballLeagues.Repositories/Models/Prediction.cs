#nullable disable
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VNFootballLeagues.Repositories.Models;

[Table("Predictions")]
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
