#nullable disable
using System;

namespace VNFootballLeagues.Repositories.Models;

public partial class UserReward
{
    public int UserRewardId { get; set; }

    public DateTime? AwardedAt { get; set; }

    public Guid? UserId { get; set; }

    public int? RewardId { get; set; }

    public virtual User User { get; set; }

    public virtual Reward Reward { get; set; }
}
