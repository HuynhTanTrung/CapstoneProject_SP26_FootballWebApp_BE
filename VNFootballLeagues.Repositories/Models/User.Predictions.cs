#nullable disable
using System.Collections.Generic;

namespace VNFootballLeagues.Repositories.Models;

public partial class User
{
    public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();

    public virtual ICollection<UserReward> UserRewards { get; set; } = new List<UserReward>();

    public virtual UserPredictionStats UserPredictionStats { get; set; }
}
