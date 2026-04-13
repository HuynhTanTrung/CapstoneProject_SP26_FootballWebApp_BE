using Microsoft.EntityFrameworkCore;

namespace VNFootballLeagues.Repositories.Models;

public partial class VNFootballLeaguesDBContext
{
    public virtual DbSet<AIAnalysisHistory> AIAnalysisHistories { get; set; }
}
