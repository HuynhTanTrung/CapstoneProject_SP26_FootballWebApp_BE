#nullable disable
using System;
using System.Collections.Generic;

namespace VNFootballLeagues.Repositories.Models;

public partial class Reward
{
    public int RewardId { get; set; }

    public string RewardName { get; set; }

    public string Description { get; set; }

    public int? RequiredCorrectPredictions { get; set; }

    public string IconUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<UserReward> UserRewards { get; set; } = new List<UserReward>();
}
