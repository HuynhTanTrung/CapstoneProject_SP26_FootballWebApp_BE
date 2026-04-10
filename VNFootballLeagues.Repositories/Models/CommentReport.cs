namespace VNFootballLeagues.Repositories.Models;

public class CommentReport
{
    public int ReportId { get; set; }
    public int CommentId { get; set; }
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>pending | reviewed | dismissed</summary>
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ForumComment Comment { get; set; } = null!;
    public virtual User Reporter { get; set; } = null!;
}
