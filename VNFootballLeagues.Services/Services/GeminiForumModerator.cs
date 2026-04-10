using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services;

public class GeminiForumModerator : IGeminiForumModerator
{
    private readonly IGeminiService _gemini;

    public GeminiForumModerator(IGeminiService gemini) => _gemini = gemini;

    public async Task<(bool Relevant, string Reason)> CheckPostRelevanceAsync(string title, string content, string leagueTag)
    {
        var prompt = $"""
            Bạn là AI kiểm duyệt nội dung cho diễn đàn bóng đá Việt Nam.
            Chỉ cho phép bài đăng liên quan đến V-League 1, V-League 2, hoặc Vietnam Cup.
            
            Tiêu đề: {title}
            Nội dung: {content}
            Giải đấu được chọn: {leagueTag}
            
            Trả lời CHÍNH XÁC theo format:
            RELEVANT: true/false
            REASON: lý do ngắn gọn (tiếng Việt, tối đa 100 ký tự)
            """;

        try
        {
            var response = await _gemini.ChatAsync(prompt);
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool relevant = true;
            string reason = "Nội dung phù hợp.";

            foreach (var line in lines)
            {
                if (line.StartsWith("RELEVANT:", StringComparison.OrdinalIgnoreCase))
                    relevant = line.Contains("true", StringComparison.OrdinalIgnoreCase);
                if (line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase))
                    reason = line.Substring(7).Trim();
            }
            return (relevant, reason);
        }
        catch { return (true, "Không thể kiểm tra tự động."); }
    }

    public async Task<(bool Toxic, string Reason)> CheckCommentToxicityAsync(string content)
    {
        // Quick local filter for obvious bad words
        var badWords = new[] { "đm", "vcl", "vl", "clgt", "đcm", "địt", "lồn", "cặc", "buồi", "đéo", "đụ", "đít",
            "óc chó", "đần độn", "khốn nạn", "mẹ mày", "bố mày", "súc vật", "chó đẻ", "thằng chó", "con chó",
            "mày ngu", "thằng ngu", "con ngu", "đồ ngu", "ngu vl", "ngu vcl" };
        var lower = content.ToLower();
        if (badWords.Any(w => lower.Contains(w)))
            return (true, "Bình luận chứa từ ngữ thô tục.");

        var prompt = $"""
            Bạn là AI kiểm duyệt bình luận cho diễn đàn bóng đá Việt Nam.
            Phát hiện các bình luận vi phạm sau:
            - Từ ngữ thô tục, tục tĩu tiếng Việt (ví dụ: ngu, đần, óc chó, mẹ mày, đm, vcl, vl, clgt, đcm, địt, lồn, cặc, buồi, chó, súc vật dùng để chửi người)
            - Xúc phạm, miệt thị cầu thủ, HLV, đội bóng, ban tổ chức
            - Kỳ thị vùng miền, dân tộc, giới tính
            - Đe dọa, kích động bạo lực
            - Spam, quảng cáo không liên quan
            
            Bình luận: {content}
            
            Trả lời CHÍNH XÁC theo format:
            TOXIC: true/false
            REASON: lý do ngắn gọn (tiếng Việt, tối đa 100 ký tự)
            """;

        try
        {
            var response = await _gemini.ChatAsync(prompt);
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool toxic = false;
            string reason = "";

            foreach (var line in lines)
            {
                if (line.StartsWith("TOXIC:", StringComparison.OrdinalIgnoreCase))
                    toxic = line.Contains("true", StringComparison.OrdinalIgnoreCase);
                if (line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase))
                    reason = line.Substring(7).Trim();
            }
            return (toxic, reason);
        }
        catch { return (false, ""); }
    }
}
