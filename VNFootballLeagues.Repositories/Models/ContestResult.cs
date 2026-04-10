#nullable disable
using System;
using System.ComponentModel.DataAnnotations;

namespace VNFootballLeagues.Repositories.Models;

public partial class ContestResult
{
    [Key]
    public int ResultId { get; set; }

    public int ContestId { get; set; }

    public int Rank { get; set; } = 1;

    public int? TeamId { get; set; }

    public int? PlayerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual PredictionContest Contest { get; set; }
    public virtual Team Team { get; set; }
    public virtual Player Player { get; set; }
}
