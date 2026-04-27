using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services;

public class ArticleAnalysisService : IArticleAnalysisService
{
    private const string SYSTEM_PROMPT = """
        Bạn là chuyên gia phân tích bóng đá Việt Nam của hệ thống VN Player Rating — nền tảng đánh giá cầu thủ và phân tích chuyên sâu bóng đá Việt Nam.

        === NHIỆM VỤ ===
        Phân tích bài viết bóng đá được cung cấp. Bài viết sẽ bao gồm: tiêu đề, URL nguồn, và nội dung đầy đủ.

        === PHẠM VI HỖ TRỢ ===
        Hệ thống CHỈ hỗ trợ phân tích bài viết liên quan đến 3 giải đấu bóng đá Việt Nam:
          • V-League 1 (Giải Vô địch Quốc gia Việt Nam — hạng cao nhất)
          • V-League 2 (Giải Hạng Nhất Việt Nam)
          • Vietnam Cup (Cúp Quốc gia Việt Nam)

        === BƯỚC 1: NHẬN DIỆN GIẢI ĐẤU ===
        Đọc kỹ tiêu đề, URL và nội dung bài viết. Tìm kiếm CÁC DẤU HIỆU SAU:

        DẤU HIỆU BÀI VIẾT THUỘC 3 GIẢI VIỆT NAM (nếu thấy BẤT KỲ dấu hiệu nào → đây là bài viết hợp lệ):
          • Tên giải đấu xuất hiện trực tiếp: "V-League", "V.League", "V League", "Hạng Nhất", "Cúp Quốc Gia", "Vietnam Cup"
          • Tên CLB Việt Nam: Công An Hà Nội (CAHN), Hà Nội FC, Hoàng Anh Gia Lai (HAGL), Thép Xanh Nam Định,
            SHB Đà Nẵng, Becamex Bình Dương, Quảng Nam FC, Sông Lam Nghệ An (SLNA), Đông Á Thanh Hóa,
            TP.HCM FC, Long An FC, Khánh Hòa FC, PVF-CAND, Hải Phòng FC, Viettel FC, Bình Định FC,
            Hà Tĩnh FC, Đà Nẵng FC, Cần Thơ FC, An Giang FC, Phú Thọ FC, v.v.
          • Tên cầu thủ Việt Nam nổi tiếng: Nguyễn Quang Hải, Nguyễn Văn Toàn, Tiến Linh, Văn Lâm,
            Đình Trọng, Hùng Dũng, Tuấn Hải, Văn Thanh, Hoàng Đức, Công Phượng, v.v.
          • Tên HLV Việt Nam: Kim Sang-sik, Troussier, Calisto, Miura, Park Hang-seo (khi dẫn dắt ĐT Việt Nam), v.v.
          • Địa danh sân vận động Việt Nam: Mỹ Đình, Hàng Đẫy, Thống Nhất, Thiên Trường, Quy Nhơn,
            Pleiku, Vinh, Thanh Hóa, Bình Dương, Cần Thơ, v.v.
          • Từ khóa đặc trưng: "bóng đá Việt Nam", "BĐVN", "VFF", "VPF", "giải VĐQG", "mùa giải [năm] Việt Nam"
          • URL từ các trang báo thể thao Việt Nam: bongdaplus.vn, bongda.com.vn, vnexpress.net/the-thao,
            tuoitre.vn/the-thao, thanhnien.vn/the-thao, dantri.com.vn/the-thao, soha.vn/bong-da, v.v.

        DẤU HIỆU BÀI VIẾT THUỘC GIẢI NGOẠI (chỉ kết luận là giải ngoại khi có BẰNG CHỨNG RÕ RÀNG):
          • Tên giải ngoại xuất hiện rõ ràng: Premier League, La Liga, Bundesliga, Serie A, Ligue 1,
            Champions League, Europa League, World Cup, Asian Cup, AFF Cup (đội tuyển quốc gia), v.v.
          • Tên CLB nước ngoài nổi tiếng: Manchester City, Liverpool, Real Madrid, Barcelona, Bayern Munich,
            PSG, Juventus, Chelsea, Arsenal, Inter Milan, AC Milan, Atletico Madrid, v.v.
          • Bài viết hoàn toàn không đề cập đến bất kỳ yếu tố Việt Nam nào

        === BƯỚC 2: RA QUYẾT ĐỊNH ===

        TRƯỜNG HỢP A — Bài viết rõ ràng thuộc V-League 1 / V-League 2 / Vietnam Cup:
        → Ghi ở đầu phân tích: [GIẢI ĐẤU: V-League 1] hoặc [GIẢI ĐẤU: V-League 2] hoặc [GIẢI ĐẤU: Vietnam Cup]
        → TIẾN HÀNH PHÂN TÍCH ĐẦY ĐỦ theo Bước 3.

        TRƯỜNG HỢP B — Bài viết về bóng đá Việt Nam nhưng KHÔNG XÁC ĐỊNH được giải cụ thể:
        → Ghi ở đầu: [GIẢI ĐẤU: Bóng đá Việt Nam — không xác định giải cụ thể]
        → TIẾN HÀNH PHÂN TÍCH ĐẦY ĐỦ theo Bước 3.

        TRƯỜNG HỢP C — Bài viết về giải đấu NƯỚC NGOÀI (có bằng chứng rõ ràng):
        → TỪ CHỐI. Trả lời CHÍNH XÁC câu sau (không thêm bớt):
        "Bài viết này thuộc giải đấu nước ngoài. Hệ thống VN Player Rating chỉ hỗ trợ phân tích bài viết từ V-League 1, V-League 2 và Vietnam Cup."

        TRƯỜNG HỢP D — Nội dung KHÔNG PHẢI bóng đá:
        → TỪ CHỐI. Trả lời CHÍNH XÁC câu sau (không thêm bớt):
        "Bài viết này không phải nội dung bóng đá. Hệ thống chỉ hỗ trợ phân tích bài viết về V-League 1, V-League 2 và Vietnam Cup."

        === BƯỚC 3: NỘI DUNG PHÂN TÍCH (chỉ khi thuộc Trường hợp A hoặc B) ===

        Trình bày theo bố cục markdown sau:

        ## Tóm tắt bài viết
        - 3-5 câu tóm gọn nội dung chính của bài viết
        - Nêu rõ: trận đấu/sự kiện nào, đội nào, kết quả/diễn biến chính

        ## Phân tích chuyên sâu

        ### Chiến thuật & Lối chơi
        - Nhận xét về chiến thuật, sơ đồ đội hình được đề cập
        - Điểm mạnh/yếu trong lối chơi của các đội
        - So sánh phong độ nếu bài viết có dữ liệu

        ### Cầu thủ nổi bật
        - Phân tích màn trình diễn của các cầu thủ được nhắc đến
        - Đánh giá đóng góp cụ thể (bàn thắng, kiến tạo, phòng thủ, v.v.)
        - Nhận xét về phong độ hiện tại

        ### Bối cảnh & Ý nghĩa
        - Ý nghĩa của trận đấu/sự kiện với bảng xếp hạng
        - Tác động đến cuộc đua vô địch / trụ hạng / cúp
        - So sánh với các trận trước (nếu bài viết đề cập)

        ## Dự đoán & Nhận định
        - 2-3 nhận định về triển vọng sắp tới của các đội liên quan
        - Dự đoán có căn cứ dựa trên dữ liệu trong bài viết
        - Điểm cần theo dõi trong các trận tiếp theo

        ## Điểm nổi bật
        - 3-5 bullet point tóm tắt những điểm quan trọng nhất để người đọc nắm nhanh

        ## Khám phá thêm
        Sau phần phân tích, thêm một block JSON duy nhất theo định dạng sau (KHÔNG thêm markdown code fence, chỉ JSON thuần):
        <!--ENTITIES:{"players":["Tên cầu thủ 1","Tên cầu thủ 2"],"teams":["Tên CLB 1","Tên CLB 2"]}-->

        Liệt kê tối đa 5 cầu thủ và 4 CLB được nhắc đến nhiều nhất hoặc quan trọng nhất trong bài (bao gồm cả đội đối thủ nếu có). Nếu không có thì để mảng rỗng [].

        === LƯU Ý QUAN TRỌNG ===
        • Chỉ phân tích dựa trên thông tin có trong bài viết, KHÔNG bịa đặt số liệu.
        • Nếu bài viết thiếu thông tin cho một mục, ghi "Không có đủ thông tin trong bài viết".
        • Sử dụng ngôn ngữ chuyên môn bóng đá tiếng Việt, rõ ràng và có cấu trúc.
        • Độ dài phân tích: 400-600 từ.
        • KHÔNG lặp lại nguyên văn nội dung bài viết, hãy phân tích và bình luận.
        • Luôn ưu tiên phân tích khi không chắc chắn — chỉ từ chối khi có bằng chứng rõ ràng.
        """;

    private readonly VNFootballLeaguesDBContext _db;
    private readonly IGeminiService _gemini;
    private readonly ILogger<ArticleAnalysisService> _logger;

    public ArticleAnalysisService(
        VNFootballLeaguesDBContext db,
        IGeminiService gemini,
        ILogger<ArticleAnalysisService> logger)
    {
        _db = db;
        _gemini = gemini;
        _logger = logger;
    }

    private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private static DateTime TodayVN() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz).Date;

    public async Task<ArticleAnalysisResponse> AnalyzeArticleAsync(
        ArticleAnalysisRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        // 1. Check active subscription (extension feature requires a paid plan)
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var isActive = sub != null && sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && sub.ExpiresAt > DateTime.UtcNow;

        if (!isActive)
        {
            return new ArticleAnalysisResponse(false, "PREMIUM_REQUIRED", null,
                "Tính năng này yêu cầu gói Premium. Vui lòng nâng cấp tài khoản tại VN Football Analytics.");
        }

        // 2. Check article credits (fixed pool, not daily-reset)
        if (sub!.AiArticleCreditsRemaining <= 0)
        {
            return new ArticleAnalysisResponse(false, "NO_CREDITS", null,
                "Bạn đã hết lượt phân tích bài viết AI. Vui lòng nạp thêm lượt tại VN Football Analytics.",
                CreditsRemaining: 0);
        }

        // 3. Validate input
        if (string.IsNullOrWhiteSpace(request.ArticleContent) || request.ArticleContent.Length < 100)
        {
            return new ArticleAnalysisResponse(false, "INVALID_CONTENT", null,
                "Nội dung bài viết quá ngắn hoặc không hợp lệ.");
        }

        // 3. Truncate content to avoid token overflow (max ~4000 chars for speed)
        var content = request.ArticleContent.Length > 4000
            ? request.ArticleContent[..4000] + "\n[... nội dung bị cắt bớt ...]"
            : request.ArticleContent;

        var userMessage = $"""
            Tiêu đề: {request.ArticleTitle}
            URL: {request.ArticleUrl}

            Nội dung bài viết:
            {content}
            """;

        ct.ThrowIfCancellationRequested();

        // Use a 50s timeout for article analysis to avoid hanging
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(50));

        var result = await _gemini.ChatWithSystemContextAsync(
            SYSTEM_PROMPT,
            [("user", userMessage)]);

        var isRefused = result.Contains("Bài viết này thuộc giải đấu nước ngoài")
                     || result.Contains("Bài viết này không phải nội dung bóng đá");

        var isError = result.StartsWith("Lỗi", StringComparison.OrdinalIgnoreCase)
                   || result.StartsWith("⚠️", StringComparison.Ordinal);

        if (isError)
        {
            return new ArticleAnalysisResponse(false, result, null, null);
        }

        // 4. Extract detected league from response
        string? detectedLeague = null;
        if (result.Contains("[GIẢI ĐẤU: V-League 1]")) detectedLeague = "V-League 1";
        else if (result.Contains("[GIẢI ĐẤU: V-League 2]")) detectedLeague = "V-League 2";
        else if (result.Contains("[GIẢI ĐẤU: Vietnam Cup]")) detectedLeague = "Vietnam Cup";
        else if (result.Contains("[GIẢI ĐẤU:")) detectedLeague = "Bóng đá Việt Nam";

        // 5. Parse entities first, then save history with entities included
        ArticleEntities? entities = null;
        var entityMatch = System.Text.RegularExpressions.Regex.Match(
            result, @"<!--ENTITIES:(\{.*?\})-->", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (entityMatch.Success)
        {
            try
            {
                var entityJson = entityMatch.Groups[1].Value;
                using var doc = System.Text.Json.JsonDocument.Parse(entityJson);
                var playerNames = doc.RootElement.GetProperty("players")
                    .EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
                var teamNames = doc.RootElement.GetProperty("teams")
                    .EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();

                var playerLinks = new List<string>();
                foreach (var name in playerNames.Take(5))
                {
                    var player = await _db.Players
                        .Where(p => p.FullName.Contains(name) || name.Contains(p.FullName))
                        .Select(p => new { p.PlayerId, p.FullName })
                        .FirstOrDefaultAsync(ct);
                    if (player != null)
                        playerLinks.Add($"{player.FullName}|{player.PlayerId}");
                }

                var teamLinks = new List<string>();
                foreach (var name in teamNames.Take(4))
                {
                    var team = await _db.Teams
                        .Where(t => t.TeamName.Contains(name) || name.Contains(t.TeamName))
                        .Select(t => new { t.TeamId, t.TeamName })
                        .FirstOrDefaultAsync(ct);
                    if (team != null)
                        teamLinks.Add($"{team.TeamName}|{team.TeamId}");
                }

                entities = new ArticleEntities(playerLinks, teamLinks);
            }
            catch { /* ignore parse errors */ }
        }

        if (!isRefused)
        {
            _db.AIAnalysisHistories.Add(new AIAnalysisHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AnalysisType = "article",
                MatchId = 0,
                PlayerId = null,
                AnalysisVi = result,
                ContextJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    articleUrl = request.ArticleUrl,
                    articleTitle = request.ArticleTitle,
                    detectedLeague,
                    entities = entities == null ? null : new
                    {
                        players = entities.Players,
                        teams = entities.Teams
                    }
                }),
                CreatedAt = DateTime.UtcNow
            });
            sub!.AiArticleCreditsRemaining = Math.Max(0, sub.AiArticleCreditsRemaining - 1);
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Article analysis for user {UserId}: league={League}, refused={Refused}",
            userId, detectedLeague, isRefused);

        // Remove the entities comment from displayed analysis
        var cleanAnalysis = System.Text.RegularExpressions.Regex.Replace(
            result, @"\s*<!--ENTITIES:.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        return new ArticleAnalysisResponse(!isRefused, cleanAnalysis, detectedLeague, null, entities,
            CreditsRemaining: isRefused ? sub!.AiArticleCreditsRemaining : Math.Max(0, sub!.AiArticleCreditsRemaining - 1));
    }
}
