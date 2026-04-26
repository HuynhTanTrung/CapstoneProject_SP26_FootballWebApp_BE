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

    public virtual DbSet<DailyCheckIn> DailyCheckIns { get; set; }

    public virtual DbSet<DailyChatUsage> DailyChatUsages { get; set; }

    public virtual DbSet<DailyAiAnalysisUsage> DailyAiAnalysisUsages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<CosmeticItem> CosmeticItems { get; set; }
    public virtual DbSet<UserCosmetic> UserCosmetics { get; set; }
    public virtual DbSet<UserLoadout> UserLoadouts { get; set; }
}
