namespace VNFootballLeagues.Repositories.Models;

public class DailyCheckIn
{
    public int CheckInId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Date only (UTC date, no time component)</summary>
    public DateTime CheckInDate { get; set; }
    public int Streak { get; set; }
    public int PointsEarned { get; set; }
    public virtual User User { get; set; } = null!;
}
