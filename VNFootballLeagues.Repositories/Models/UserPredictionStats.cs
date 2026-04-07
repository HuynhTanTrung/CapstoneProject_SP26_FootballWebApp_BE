#nullable disable
using System;

namespace VNFootballLeagues.Repositories.Models;

public partial class UserPredictionStats
{
    public Guid UserId { get; set; }

    public int? TotalPredictions { get; set; }

    public int? CorrectPredictions { get; set; }

    public int? ExactScorePredictions { get; set; }

    public int? Points { get; set; }

    public DateTime? LastUpdated { get; set; }

    public virtual User User { get; set; }
}
