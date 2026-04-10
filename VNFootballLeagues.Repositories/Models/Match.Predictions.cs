#nullable disable
using System.Collections.Generic;

namespace VNFootballLeagues.Repositories.Models;

public partial class Match
{
    public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
