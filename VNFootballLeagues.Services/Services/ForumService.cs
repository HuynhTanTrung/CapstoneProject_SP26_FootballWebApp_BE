using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Services;

public class ForumService
{
    private readonly VNFootballLeaguesDBContext _db;
    private readonly IGeminiForumModerator _moderator;

    public ForumService(VNFootballLeaguesDBContext db, IGeminiForumModerator moderator)
    {
        _db = db;
        _moderator = moderator;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────
    public record PostSummaryDto(int PostId, string Title, string? LeagueTag, string Status,
        string AuthorName, string? AuthorAvatar, DateTime CreatedAt, int CommentCount, string? FirstMediaUrl, string? RejectionReason, int ViewCount);

    public record PostDetailDto(int PostId, string Title, string Content, string? LeagueTag,
        string Status, string? RejectionReason, string AuthorName, string? AuthorAvatar,
        Guid AuthorId, DateTime CreatedAt, List<string> MediaUrls, List<string> MediaTypes, int CommentCount,
        int ViewCount, Dictionary<string, int> Reactions, string? AuthorNameColorPreview, string? AuthorFramePreview, string? AuthorBadgePreview, string? AuthorEffectPreview);

    public record CommentDto(int CommentId, Guid UserId, string AuthorName, string? AuthorAvatar,
        string Content, string Status, DateTime CreatedAt, int? ParentCommentId, string? ParentAuthorName,
        string? AuthorNameColorPreview, string? AuthorFramePreview, string? AuthorBadgePreview,
        string? ParentAuthorNameColorPreview);

    public record ReactionSummaryDto(Dictionary<string, int> Counts, string? MyReaction);

    // ── Posts ─────────────────────────────────────────────────────────────
    public async Task<(List<PostSummaryDto> Items, int Total)> GetPublicPostsAsync(
        string? leagueTag, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ForumPosts.Include(p => p.User).Include(p => p.Comments)
            .Where(p => p.Status == "approved");
        if (!string.IsNullOrWhiteSpace(leagueTag)) q = q.Where(p => p.LeagueTag == leagueTag);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items.Select(MapSummary).ToList(), total);
    }

    public async Task<PostDetailDto?> GetPostAsync(int postId, CancellationToken ct = default)
    {
        var p = await _db.ForumPosts.Include(p => p.User).Include(p => p.Comments)
            .FirstOrDefaultAsync(p => p.PostId == postId, ct);
        if (p == null) return null;

        // Increment view count
        p.ViewCount++;
        await _db.SaveChangesAsync(ct);

        // Get reactions
        var reactions = await _db.ForumReactions.Where(r => r.PostId == postId)
            .GroupBy(r => r.ReactionType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

        // Get author loadout
        var loadout = await _db.UserLoadouts.FirstOrDefaultAsync(l => l.UserId == p.UserId, ct);
        string? nameColor = null, frame = null, badge = null, effect = null;
        if (loadout != null)
        {
            var ids = new[] { loadout.NameColorItemId, loadout.FrameItemId, loadout.BadgeItemId, loadout.EffectItemId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var items = await _db.CosmeticItems.Where(i => ids.Contains(i.ItemId)).ToDictionaryAsync(i => i.ItemId, ct);
            if (loadout.NameColorItemId.HasValue && items.TryGetValue(loadout.NameColorItemId.Value, out var nc)) nameColor = nc.PreviewData;
            if (loadout.FrameItemId.HasValue && items.TryGetValue(loadout.FrameItemId.Value, out var fr)) frame = fr.PreviewData;
            if (loadout.BadgeItemId.HasValue && items.TryGetValue(loadout.BadgeItemId.Value, out var bd)) badge = bd.PreviewData;
            if (loadout.EffectItemId.HasValue && items.TryGetValue(loadout.EffectItemId.Value, out var ef)) effect = ef.PreviewData;
        }

        return MapDetail(p, reactions, nameColor, frame, badge, effect);
    }

    public async Task<List<PostSummaryDto>> GetMyPostsAsync(Guid userId, CancellationToken ct = default)
    {
        var posts = await _db.ForumPosts.Include(p => p.User).Include(p => p.Comments)
            .Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return posts.Select(MapSummary).ToList();
    }

    public async Task<(bool Success, string Message, int? PostId)> CreatePostAsync(
        Guid userId, string title, string content, string leagueTag,
        List<string> mediaUrls, List<string> mediaTypes, CancellationToken ct = default)
    {
        // Check subscription + forum credits
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        bool isActive = sub != null && sub.Status == "Active" && sub.ExpiresAt > DateTime.UtcNow;
        if (!isActive) return (false, "Bạn cần đăng ký gói để đăng bài.", null);
        if (sub!.ForumPostCreditsRemaining <= 0) return (false, "Bạn đã hết lượt đăng bài.", null);

        var post = new ForumPost
        {
            UserId = userId,
            Title = title,
            Content = content,
            LeagueTag = leagueTag,
            MediaUrls = mediaUrls.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(mediaUrls) : null,
            MediaTypes = mediaTypes.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(mediaTypes) : null,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ForumPosts.Add(post);
        sub.ForumPostCreditsRemaining--;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // AI moderation async (fire and forget style - update after)
        _ = Task.Run(async () =>
        {
            try
            {
                var (relevant, reason) = await _moderator.CheckPostRelevanceAsync(title, content, leagueTag);
                post.AiChecked = true;
                post.AiScore = relevant ? 0.8 : 0.1;
                post.Status = relevant ? "approved" : "rejected";
                post.RejectionReason = relevant ? null : reason;
                post.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            catch { /* ignore */ }
        });

        return (true, "Bài đăng đang chờ kiểm duyệt.", post.PostId);
    }

    // ── Admin ─────────────────────────────────────────────────────────────
    public async Task<(List<PostSummaryDto> Items, int Total)> GetAdminPostsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ForumPosts.Include(p => p.User).Include(p => p.Comments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items.Select(MapSummary).ToList(), total);
    }

    public async Task<bool> ApprovePostAsync(int postId, CancellationToken ct = default)
    {
        var p = await _db.ForumPosts.FindAsync(new object[] { postId }, ct);
        if (p == null) return false;
        p.Status = "approved"; p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> RejectPostAsync(int postId, string reason, CancellationToken ct = default)
    {
        var p = await _db.ForumPosts.FindAsync(new object[] { postId }, ct);
        if (p == null) return false;
        p.Status = "rejected"; p.RejectionReason = reason; p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> HidePostAsync(int postId, CancellationToken ct = default)
    {
        var p = await _db.ForumPosts.FindAsync(new object[] { postId }, ct);
        if (p == null) return false;
        p.Status = "hidden"; p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); return true;
    }

    // ── Comments ──────────────────────────────────────────────────────────
    public async Task<List<CommentDto>> GetCommentsAsync(int postId, CancellationToken ct = default)
    {
        var comments = await _db.ForumComments.Include(c => c.User)
            .Where(c => c.PostId == postId && c.Status == "active")
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);

        // Get parent author names
        var parentIds = comments.Where(c => c.ParentCommentId.HasValue).Select(c => c.ParentCommentId!.Value).Distinct().ToList();
        var parentAuthors = parentIds.Any()
            ? await _db.ForumComments.Include(c => c.User).Where(c => parentIds.Contains(c.CommentId))
                .ToDictionaryAsync(c => c.CommentId, c => c.User?.FullName ?? c.User?.Username ?? "?", ct)
            : new Dictionary<int, string>();

        // Get loadouts for all comment authors
        var userIds = comments.Select(c => c.UserId).Distinct().ToList();
        var loadouts = await _db.UserLoadouts.Where(l => userIds.Contains(l.UserId)).ToListAsync(ct);
        var itemIds = loadouts.SelectMany(l => new[] { l.NameColorItemId, l.FrameItemId, l.BadgeItemId }.Where(id => id.HasValue).Select(id => id!.Value)).Distinct().ToList();
        var items = itemIds.Any() ? await _db.CosmeticItems.Where(i => itemIds.Contains(i.ItemId)).ToDictionaryAsync(i => i.ItemId, ct) : new Dictionary<int, CosmeticItem>();

        return comments.Select(c => {
            var lo = loadouts.FirstOrDefault(l => l.UserId == c.UserId);
            string? nc = lo?.NameColorItemId.HasValue == true && items.TryGetValue(lo.NameColorItemId!.Value, out var nci) ? nci.PreviewData : null;
            string? fr = lo?.FrameItemId.HasValue == true && items.TryGetValue(lo.FrameItemId!.Value, out var fri) ? fri.PreviewData : null;
            string? bd = lo?.BadgeItemId.HasValue == true && items.TryGetValue(lo.BadgeItemId!.Value, out var bdi) ? bdi.PreviewData : null;
            string? parentName = c.ParentCommentId.HasValue && parentAuthors.TryGetValue(c.ParentCommentId.Value, out var pn) ? pn : null;

            // Parent author name color
            string? parentNc = null;
            if (c.ParentCommentId.HasValue)
            {
                var parentComment = comments.FirstOrDefault(x => x.CommentId == c.ParentCommentId.Value);
                if (parentComment != null)
                {
                    var plo = loadouts.FirstOrDefault(l => l.UserId == parentComment.UserId);
                    if (plo?.NameColorItemId.HasValue == true && items.TryGetValue(plo.NameColorItemId!.Value, out var pnci))
                        parentNc = pnci.PreviewData;
                }
            }

            return new CommentDto(c.CommentId, c.UserId, c.User?.FullName ?? c.User?.Username ?? "?",
                c.User?.AvatarUrl, c.Content, c.Status, c.CreatedAt, c.ParentCommentId, parentName, nc, fr, bd, parentNc);
        }).ToList();
    }

    public async Task<(bool Success, string Message)> AddCommentAsync(
        int postId, Guid userId, string content, int? parentCommentId, CancellationToken ct = default)
    {
        var ban = await _db.UserCommentBans
            .Where(b => b.UserId == userId && b.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(b => b.ExpiresAt).FirstOrDefaultAsync(ct);
        if (ban != null)
            return (false, $"Bạn bị cấm bình luận đến {ban.ExpiresAt:dd/MM/yyyy HH:mm}.");

        var post = await _db.ForumPosts.FindAsync(new object[] { postId }, ct);
        if (post == null || post.Status != "approved")
            return (false, "Bài đăng không tồn tại hoặc chưa được duyệt.");

        var comment = new ForumComment
        {
            PostId = postId, UserId = userId, Content = content,
            ParentCommentId = parentCommentId,
            Status = "active", CreatedAt = DateTime.UtcNow
        };
        _db.ForumComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        _ = Task.Run(async () =>
        {
            try
            {
                var (toxic, reason) = await _moderator.CheckCommentToxicityAsync(content);
                if (toxic)
                {
                    comment.Status = "hidden";
                    comment.AiWarningCount++;
                    var warningCount = await _db.ForumComments
                        .CountAsync(c => c.UserId == userId && c.AiWarningCount > 0);
                    if (warningCount >= 2)
                    {
                        _db.UserCommentBans.Add(new UserCommentBan
                        {
                            UserId = userId, Reason = "Bình luận vi phạm cộng đồng nhiều lần.",
                            BannedBy = "AI", BannedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(2)
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch { }
        });

        return (true, "Đã thêm bình luận.");
    }

    // ── Reactions ─────────────────────────────────────────────────────────
    public async Task<(bool Success, string? MyReaction, Dictionary<string, int> Counts)> ToggleReactionAsync(
        int postId, Guid userId, string reactionType, CancellationToken ct = default)
    {
        var existing = await _db.ForumReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);
        if (existing != null)
        {
            if (existing.ReactionType == reactionType)
            {
                _db.ForumReactions.Remove(existing); // toggle off
            }
            else
            {
                existing.ReactionType = reactionType; // change reaction
            }
        }
        else
        {
            _db.ForumReactions.Add(new ForumReaction { PostId = postId, UserId = userId, ReactionType = reactionType });
        }
        await _db.SaveChangesAsync(ct);

        var counts = await _db.ForumReactions.Where(r => r.PostId == postId)
            .GroupBy(r => r.ReactionType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

        var myReaction = await _db.ForumReactions
            .Where(r => r.PostId == postId && r.UserId == userId)
            .Select(r => r.ReactionType).FirstOrDefaultAsync(ct);

        return (true, myReaction, counts);
    }

    public async Task<(Dictionary<string, int> Counts, string? MyReaction)> GetReactionsAsync(
        int postId, Guid? userId, CancellationToken ct = default)
    {
        var counts = await _db.ForumReactions.Where(r => r.PostId == postId)
            .GroupBy(r => r.ReactionType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, ct);
        string? myReaction = null;
        if (userId.HasValue)
            myReaction = await _db.ForumReactions.Where(r => r.PostId == postId && r.UserId == userId.Value)
                .Select(r => r.ReactionType).FirstOrDefaultAsync(ct);
        return (counts, myReaction);
    }

    public async Task<bool> HideCommentAsync(int commentId, CancellationToken ct = default)
    {
        var c = await _db.ForumComments.FindAsync(new object[] { commentId }, ct);
        if (c == null) return false;
        c.Status = "hidden"; await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<(bool Success, string Message)> EditCommentAsync(int commentId, Guid userId, string content, CancellationToken ct = default)
    {
        var c = await _db.ForumComments.FindAsync(new object[] { commentId }, ct);
        if (c == null) return (false, "Không tìm thấy bình luận.");
        if (c.UserId != userId) return (false, "Không có quyền chỉnh sửa.");
        c.Content = content;
        await _db.SaveChangesAsync(ct);
        return (true, "Đã cập nhật bình luận.");
    }

    public async Task<(bool Success, string Message)> DeleteCommentAsync(int commentId, Guid userId, CancellationToken ct = default)
    {
        var c = await _db.ForumComments.FindAsync(new object[] { commentId }, ct);
        if (c == null) return (false, "Không tìm thấy bình luận.");
        if (c.UserId != userId) return (false, "Không có quyền xóa.");
        _db.ForumComments.Remove(c);
        await _db.SaveChangesAsync(ct);
        return (true, "Đã xóa bình luận.");
    }

    public async Task<bool> BanUserCommentAsync(Guid userId, string reason, int days, CancellationToken ct = default)
    {
        _db.UserCommentBans.Add(new UserCommentBan
        {
            UserId = userId, Reason = reason, BannedBy = "Admin",
            BannedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(days)
        });
        await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> UnbanUserCommentAsync(Guid userId, CancellationToken ct = default)
    {
        var bans = await _db.UserCommentBans
            .Where(b => b.UserId == userId && b.ExpiresAt > DateTime.UtcNow).ToListAsync(ct);
        _db.UserCommentBans.RemoveRange(bans);
        await _db.SaveChangesAsync(ct); return true;
    }

    // ── Mappers ───────────────────────────────────────────────────────────
    private static PostSummaryDto MapSummary(ForumPost p)
    {
        var urls = p.MediaUrls != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.MediaUrls) ?? new()
            : new List<string>();
        return new PostSummaryDto(p.PostId, p.Title, p.LeagueTag, p.Status,
            p.User?.FullName ?? p.User?.Username ?? "?",
            p.User?.AvatarUrl, p.CreatedAt, p.Comments?.Count(c => c.Status == "active") ?? 0,
            urls.FirstOrDefault(), p.RejectionReason, p.ViewCount);
    }

    private static PostDetailDto MapDetail(ForumPost p, Dictionary<string, int>? reactions = null,
        string? nameColor = null, string? frame = null, string? badge = null, string? effect = null)
    {
        var urls = p.MediaUrls != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.MediaUrls) ?? new()
            : new List<string>();
        var types = p.MediaTypes != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.MediaTypes) ?? new()
            : new List<string>();
        return new PostDetailDto(p.PostId, p.Title, p.Content, p.LeagueTag, p.Status,
            p.RejectionReason, p.User?.FullName ?? p.User?.Username ?? "?",
            p.User?.AvatarUrl, p.UserId, p.CreatedAt, urls, types,
            p.Comments?.Count(c => c.Status == "active") ?? 0,
            p.ViewCount, reactions ?? new(), nameColor, frame, badge, effect);
    }

    private static CommentDto MapComment(ForumComment c) =>
        new(c.CommentId, c.UserId, c.User?.FullName ?? c.User?.Username ?? "?",
            c.User?.AvatarUrl, c.Content, c.Status, c.CreatedAt, null, null, null, null, null, null);
}

public interface IGeminiForumModerator
{
    Task<(bool Relevant, string Reason)> CheckPostRelevanceAsync(string title, string content, string leagueTag);
    Task<(bool Toxic, string Reason)> CheckCommentToxicityAsync(string content);
}
