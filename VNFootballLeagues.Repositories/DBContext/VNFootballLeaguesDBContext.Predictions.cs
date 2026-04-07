using Microsoft.EntityFrameworkCore;

namespace VNFootballLeagues.Repositories.Models;

public partial class VNFootballLeaguesDBContext
{
    public virtual DbSet<Prediction> Predictions { get; set; }

    public virtual DbSet<Reward> Rewards { get; set; }

    public virtual DbSet<UserReward> UserRewards { get; set; }

    public virtual DbSet<UserPredictionStats> UserPredictionStats { get; set; }

    public virtual DbSet<PredictionContest> PredictionContests { get; set; }

    public virtual DbSet<ContestEntry> ContestEntries { get; set; }

    public virtual DbSet<ContestResult> ContestResults { get; set; }
}
