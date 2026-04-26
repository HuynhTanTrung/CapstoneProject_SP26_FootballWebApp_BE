namespace VNFootballLeagues.Repositories.Models;

public class DailyAiAnalysisUsage
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime UsageDate { get; set; }
    public int Count { get; set; }
    public virtual User User { get; set; } = null!;
}
