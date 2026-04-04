using System;

namespace VNFootballLeagues.Repositories.Models;

public class VideoAnalysis
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string VideoUrl { get; set; }       // Cloudinary URL
    public string VideoFileName { get; set; }  // Original filename
    public string Prompt { get; set; }
    public string Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
}
