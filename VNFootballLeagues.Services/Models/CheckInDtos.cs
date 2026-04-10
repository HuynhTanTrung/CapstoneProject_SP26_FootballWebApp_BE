namespace VNFootballLeagues.Services.Models;

public class CheckInResultDto
{
    public bool AlreadyCheckedIn { get; set; }
    public int PointsEarned { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalCheckInPoints { get; set; }
}

public class CheckInStatusDto
{
    public bool CheckedInToday { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalCheckInPoints { get; set; }
    /// <summary>List of dates (yyyy-MM-dd) user checked in this month</summary>
    public List<string> CheckedDatesThisMonth { get; set; } = new();
}
