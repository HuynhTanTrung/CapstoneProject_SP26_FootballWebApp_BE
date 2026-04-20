using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using Microsoft.AspNetCore.Authorization;

namespace VNFootballLeaguesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly IChatConversationService _chatConversation;
        private readonly VNFootballLeaguesDBContext _context;
        private readonly IUserService _userService;

        public ChatController(IGeminiService geminiService, IChatConversationService chatConversation, VNFootballLeaguesDBContext context, IUserService userService)
        {
            _geminiService = geminiService;
            _chatConversation = chatConversation;
            _context = context;
            _userService = userService;
        }

        private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        private static DateTime TodayVN() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz).Date;

        private static int GetDailyLimit(UserSubscription? sub)
        {
            if (sub == null || sub.Status != "Active" || sub.ExpiresAt <= DateTime.UtcNow)
                return 3; // Free
            return sub.PlanCode?.ToUpper() switch
            {
                "TRIAL"     => 10,
                "MONTHLY"   => 25,
                "QUARTERLY" => 50,
                _           => 3
            };
        }

        [HttpGet("chat-limit")]
        [Authorize]
        public async Task<IActionResult> GetChatLimit(CancellationToken ct)
        {
            var userId = _userService.GetUserId(User);
            if (userId is null) return Unauthorized();

            var today = TodayVN();
            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId.Value, ct);
            var limit = GetDailyLimit(sub);
            var usage = await _context.DailyChatUsages.FirstOrDefaultAsync(u => u.UserId == userId.Value && u.UsageDate == today, ct);
            int used = usage?.Count ?? 0;

            return Ok(new { limit, used, remaining = Math.Max(0, limit - used) });
        }

        /// <summary>
        /// Chat có lưu DB: gửi UserId (bắt buộc), SessionId (null = phiên mới), Message.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            if (request.UserId == Guid.Empty)
                return BadRequest(new { error = "UserId is required" });
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required" });

            // Daily chat limit check
            var today = TodayVN();
            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);
            var limit = GetDailyLimit(sub);

            var usage = await _context.DailyChatUsages.FirstOrDefaultAsync(u => u.UserId == request.UserId && u.UsageDate == today, cancellationToken);
            int usedToday = usage?.Count ?? 0;

            if (usedToday >= limit)
            {
                var planName = sub?.Status == "Active" ? sub.PlanName : "Free";
                return BadRequest(new
                {
                    error = $"Bạn đã dùng hết {limit} lượt chat hôm nay ({planName}). Vui lòng quay lại vào ngày mai hoặc nâng cấp gói.",
                    limitReached = true,
                    limit,
                    used = usedToday
                });
            }

            // Increment usage AFTER successful AI response (don't charge on error)
            try
            {
                // Inject player context vào message nếu có cầu thủ liên quan
                var systemPrompt = await BuildSystemPromptAsync(request.Message);
                var enrichedMessage = request.Message;

                // Nếu tìm được cầu thủ, thêm context vào message để ChatConversationService dùng
                if (systemPrompt.Contains("Dữ liệu cầu thủ từ hệ thống:"))
                {
                    // Gọi trực tiếp Gemini với system prompt thay vì qua ChatConversationService
                    // để có player context, nhưng vẫn lưu vào DB thủ công
                    var userExists = await _context.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken);
                    if (!userExists) return NotFound(new { error = "User not found" });

                    ChatSession session;
                    if (request.SessionId == null || request.SessionId == Guid.Empty)
                    {
                        session = new ChatSession { SessionId = Guid.NewGuid(), UserId = request.UserId, Title = request.Message.Length <= 50 ? request.Message : request.Message[..50], StartTime = DateTime.UtcNow };
                        _context.ChatSessions.Add(session);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == request.SessionId && s.UserId == request.UserId, cancellationToken)
                            ?? throw new InvalidOperationException("Session not found.");
                    }

                    _context.ChatMessages.Add(new ChatMessage { MessageId = Guid.NewGuid(), SessionId = session.SessionId, Sender = "User", Text = request.Message.Trim(), Timestamp = DateTime.UtcNow });
                    await _context.SaveChangesAsync(cancellationToken);

                    var history = await _context.ChatMessages.Where(m => m.SessionId == session.SessionId).OrderBy(m => m.Timestamp).Select(m => new { m.Sender, m.Text }).ToListAsync(cancellationToken);
                    var turns = history.Select(m => (role: m.Sender == "User" ? "user" : "model", text: m.Text ?? "")).ToList();

                    var aiResponse = await _geminiService.ChatWithSystemContextAsync(systemPrompt, turns);

                    // Deduct credit only after successful response (not on Gemini errors)
                    var isGeminiError = aiResponse.StartsWith("⚠️") || aiResponse.StartsWith("Lỗi");
                    if (!isGeminiError)
                    {
                        if (usage == null)
                            _context.DailyChatUsages.Add(new DailyChatUsage { UserId = request.UserId, UsageDate = today, Count = 1 });
                        else
                            usage.Count++;
                    }

                    _context.ChatMessages.Add(new ChatMessage { MessageId = Guid.NewGuid(), SessionId = session.SessionId, Sender = "Assistant", Text = aiResponse, Timestamp = DateTime.UtcNow });
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new { sessionId = session.SessionId, sessionTitle = session.Title, message = request.Message.Trim(), response = aiResponse });
                }

                // Không có player context → dùng ChatConversationService bình thường
                var result = await _chatConversation.SendMessageAsync(request.UserId, request.SessionId, request.Message, cancellationToken);

                // Deduct credit only after successful response (not on Gemini errors)
                var isError = result.Response.StartsWith("⚠️") || result.Response.StartsWith("Lỗi");
                if (!isError)
                {
                    if (usage == null)
                        _context.DailyChatUsages.Add(new DailyChatUsage { UserId = request.UserId, UsageDate = today, Count = 1 });
                    else
                        usage.Count++;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Ok(new { sessionId = result.SessionId, sessionTitle = result.SessionTitle, message = request.Message.Trim(), response = result.Response });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// AI hệ thống: hỏi về giải đấu, cầu thủ, trận đấu với context từ DB.
        /// </summary>
        [HttpPost("system")]
        public async Task<IActionResult> ChatSystem([FromBody] SystemChatRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required" });

            var systemPrompt = await BuildSystemPromptAsync(request.Message, request.PlayerContext);

            var turns = new List<(string role, string text)>();
            if (request.History != null)
                foreach (var h in request.History)
                    turns.Add((h.Role, h.Content));
            turns.Add(("user", request.Message));

            var response = await _geminiService.ChatWithSystemContextAsync(systemPrompt, turns);
            return Ok(new { response });
        }

        private async Task<string> BuildSystemPromptAsync(string message, string? extraContext = null)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var systemPrompt = $@"Bạn là trợ lý AI chuyên về hệ thống thống kê bóng đá Việt Nam 'VN Player Rating'.
Hệ thống theo dõi 3 giải đấu: V-League 1, V-League 2, Vietnam Cup.
GIỚI HẠN PHẠM VI: Bạn CHỈ được trả lời các câu hỏi liên quan đến bóng đá Việt Nam (V-League 1, V-League 2, Vietnam Cup, cầu thủ Việt Nam, đội bóng Việt Nam). Nếu người dùng hỏi về bất kỳ giải đấu nước ngoài nào (Premier League, La Liga, Champions League, World Cup, v.v.) hoặc cầu thủ nước ngoài không thi đấu tại Việt Nam, hãy từ chối lịch sự và nhắc lại phạm vi của hệ thống.
QUAN TRỌNG: Khi đề cập đến cầu thủ cụ thể, BẮT BUỘC phải cung cấp link dạng: [Tên cầu thủ](/players/{{playerId}}) - dùng đúng playerId số nguyên từ dữ liệu được cung cấp.
Trả lời bằng tiếng Việt, ngắn gọn và chính xác.
Ngày giờ hiện tại (UTC+7): {now:dd/MM/yyyy HH:mm}.
HƯỚNG DẪN ĐIỀU HƯỚNG: Khi người dùng hỏi về phân tích chuyên sâu trận đấu hoặc cầu thủ (ví dụ: 'phân tích trận', 'phân tích cầu thủ', 'AI phân tích', 'thống kê chi tiết trận'), hãy gợi ý họ dùng tính năng AI Phân tích chuyên sâu tại [AI Phân tích](/ai-video) để có phân tích đầy đủ hơn.
BẮT BUỘC: Dù có hay không có dữ liệu trận đấu, khi người dùng hỏi về phân tích trận đấu cụ thể, LUÔN LUÔN kết thúc câu trả lời bằng: 'Để xem phân tích chi tiết, hãy truy cập [AI Phân tích](/ai-video).'
KHI TRẢ LỜI VỀ THỐNG KÊ CẦU THỦ: Chỉ cung cấp tóm tắt ngắn gọn (rating, bàn thắng, kiến tạo, số trận). Để xem thống kê đầy đủ theo từng mùa giải, hãy gợi ý user truy cập trang cầu thủ và nâng cấp [Premium](/pricing) để xem tab Thống kê chi tiết.";

            // Tìm cầu thủ liên quan trong DB dựa trên từ khóa trong câu hỏi
            var msgLower = message.ToLower();

            // Tách các từ có độ dài >= 3 để tìm kiếm
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();

            List<Player> players = new();
            if (words.Any())
            {
                var allPlayers = await _context.Players
                    .Include(p => p.PlayerSeasonStatistics)
                    .ToListAsync();

                // Tìm theo từng từ trong tên (có dấu)
                // Tìm và sắp xếp theo số từ match nhiều nhất (ưu tiên cầu thủ match nhiều từ)
                players = allPlayers
                    .Where(p => p.FullName != null &&
                        words.Any(w => p.FullName.ToLower().Normalize().Contains(w.ToLower().Normalize())))
                    .OrderByDescending(p => words.Count(w => p.FullName!.ToLower().Normalize().Contains(w.ToLower().Normalize())))
                    .Take(5)
                    .ToList();

                // Fallback: tìm theo chuỗi liên tiếp (ví dụ "Tiến Linh" → tìm "Tiến Linh" trong tên)
                if (!players.Any() && words.Count >= 2)
                {
                    var combined = string.Join(" ", words);
                    players = allPlayers
                        .Where(p => p.FullName != null &&
                            p.FullName.ToLower().Contains(combined.ToLower()))
                        .Take(3)
                        .ToList();
                }
            }

            if (players.Any())
            {
                systemPrompt += "\n\nDữ liệu cầu thủ từ hệ thống:";
                foreach (var p in players)
                {
                    var latestStat = p.PlayerSeasonStatistics?.OrderBy(s => s.SeasonId).FirstOrDefault();
                    systemPrompt += $@"
---
Cầu thủ: {p.FullName} (ID: {p.PlayerId})
Vị trí: {p.Position} | Tuổi: {p.Age} | Quốc tịch: {p.Nationality}
Link trang cầu thủ: /players/{p.PlayerId}
Ảnh: {p.PhotoUrl}";
                    if (latestStat != null)
                    {
                        var posStats = p.Position switch
                        {
                            "G" => $"- Sạch lưới: {latestStat.CleanSheets ?? 0} | Cứu thua: {latestStat.Saves ?? 0}",
                            "D" => $"- Tắc bóng: {latestStat.Tackles ?? 0} | Cắt bóng: {latestStat.Interceptions ?? 0}",
                            _   => $"- Bàn thắng: {latestStat.Goals} | Kiến tạo: {latestStat.Assists}"
                        };
                        systemPrompt += $@"
Thống kê tổng quan (SeasonId={latestStat.SeasonId}):
- Đánh giá: {latestStat.Rating?.ToString("F1") ?? "N/A"}
- Trận đấu: {latestStat.Appearances} | Phút: {latestStat.Minutes}
{posStats}
- Thẻ vàng: {latestStat.YellowCards} | Thẻ đỏ: {latestStat.RedCards}
(Thống kê chi tiết hơn chỉ dành cho thành viên Premium)";
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(extraContext))
                systemPrompt += $"\n\nThông tin bổ sung:\n{extraContext}";

            return systemPrompt;
        }

        /// <summary>
        /// Phân tích video bóng đá qua Cloudinary URL.
        /// </summary>
        [HttpPost("analyze-video")]
        [Authorize]
        public async Task<IActionResult> AnalyzeVideo([FromBody] VideoAnalysisRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.VideoUrl))
                return BadRequest(new { error = "VideoUrl is required" });

            // Check subscription and deduct credit
            var userId = _userService.GetUserId(User);
            if (userId is null) return Unauthorized();

            var subscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId.Value);

            bool isActive = subscription != null && subscription.Status == "Active" && subscription.ExpiresAt > DateTime.UtcNow;
            if (!isActive || subscription!.AiVideoCreditsRemaining <= 0)
                return BadRequest(new { error = "Bạn không có lượt phân tích AI Video. Vui lòng nâng cấp gói.", creditsRemaining = 0 });

            // Deduct credit only after successful Gemini response
            var response = await _geminiService.AnalyzeVideoAsync(request.VideoUrl, request.Prompt ?? "");

            // Chỉ trừ credit nếu AI trả về kết quả thành công (không phải lỗi)
            bool isSuccess = !response.StartsWith("⚠️") && !response.StartsWith("Lỗi");
            if (!isSuccess)
                return StatusCode(502, new { message = response, creditsRemaining = subscription.AiVideoCreditsRemaining });

            subscription.AiVideoCreditsRemaining--;
            subscription.UpdatedAt = DateTime.UtcNow;

            var record = new VideoAnalysis
            {
                UserId = userId.Value,
                VideoUrl = request.VideoUrl,
                VideoFileName = request.VideoFileName ?? "",
                Prompt = request.Prompt ?? "",
                Result = response,
                CreatedAt = DateTime.UtcNow,
            };
            _context.VideoAnalyses.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { id = record.Id, response, creditsRemaining = subscription.AiVideoCreditsRemaining });
        }

        [HttpGet("video-history")]
        public async Task<IActionResult> GetVideoHistory([FromQuery] Guid userId)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { error = "userId is required" });

            var list = await _context.VideoAnalyses
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new {
                    v.Id, v.VideoUrl, v.VideoFileName, v.Prompt,
                    v.Result, v.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }
        [HttpPost("preview")]
        public async Task<IActionResult> ChatPreview([FromBody] ChatPreviewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message is required" });

            var response = await _geminiService.ChatAsync(request.Message);

            return Ok(new
            {
                message = request.Message,
                response
            });
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] Guid userId, CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { error = "userId query is required" });

            var list = await _chatConversation.GetSessionsAsync(userId, cancellationToken);
            return Ok(new { count = list.Count, data = list });
        }

        [HttpGet("sessions/{sessionId:guid}/messages")]
        public async Task<IActionResult> GetMessages(
            [FromRoute] Guid sessionId,
            [FromQuery] Guid userId,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { error = "userId query is required" });

            try
            {
                var list = await _chatConversation.GetMessagesAsync(userId, sessionId, cancellationToken);
                return Ok(new { sessionId, count = list.Count, data = list });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }

    public class ChatRequest
    {
        public Guid UserId { get; set; }

        /// <summary>Null hoặc 00000000-0000-0000-0000-000000000000 = tạo phiên mới.</summary>
        public Guid? SessionId { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ChatPreviewRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class SystemChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? PlayerContext { get; set; }
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; } = "user"; // "user" or "model"
        public string Content { get; set; } = string.Empty;
    }

    public class VideoAnalysisRequest
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string? VideoFileName { get; set; }
        public string? Prompt { get; set; }
        public Guid? UserId { get; set; }
    }
}
