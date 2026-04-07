using Microsoft.EntityFrameworkCore;

namespace VNFootballLeagues.Repositories.Models;

public partial class VNFootballLeaguesDBContext
{
    public virtual DbSet<Prediction> Predictions { get; set; }

    public virtual DbSet<Reward> Rewards { get; set; }

    public virtual DbSet<UserReward> UserRewards { get; set; }

    public virtual DbSet<UserPredictionStats> UserPredictionStats { get; set; }
}
