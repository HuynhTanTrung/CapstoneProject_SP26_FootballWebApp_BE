namespace VNFootballLeagues.Repositories.Models;

public class ForumPost
{
    public int PostId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>JSON array of Cloudinary URLs</summary>
    public string? MediaUrls { get; set; }
    /// <summary>JSON array: "image"|"video"</summary>
    public string? MediaTypes { get; set; }
    /// <summary>V-League 1 | V-League 2 | Vietnam Cup</summary>
    public string? LeagueTag { get; set; }
    /// <summary>pending | approved | rejected | hidden</summary>
    public string Status { get; set; } = "pending";
    public string? RejectionReason { get; set; }
    public bool AiChecked { get; set; } = false;
    public double? AiScore { get; set; }
    public int ViewCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual ICollection<ForumComment> Comments { get; set; } = new List<ForumComment>();
}

public class ForumComment
{
    public int CommentId { get; set; }
    public int PostId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    /// <summary>active | hidden | warned</summary>
    public string Status { get; set; } = "active";
    public int AiWarningCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ForumPost Post { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ForumComment? ParentComment { get; set; }
}

public class UserCommentBan
{
    public int BanId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>AI | Admin</summary>
    public string BannedBy { get; set; } = "AI";
    public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public virtual User User { get; set; } = null!;
}

public class ForumReaction
{
    public int ReactionId { get; set; }
    public int PostId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>like | love | haha | wow | angry</summary>
    public string ReactionType { get; set; } = "like";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ForumPost Post { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
