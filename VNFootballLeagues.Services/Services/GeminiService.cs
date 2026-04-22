using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private static readonly string[] _models =
        [
            "gemini-2.5-flash",
            "gemini-2.5-flash",      // retry cùng model lần 2
            "gemini-2.5-flash-lite", // fallback
        ];
        private static readonly int[] _retryDelaysMs = [0, 3000, 5000];

        public GeminiService(IConfiguration configuration)
        {
            _apiKey = ResolveApiKey(configuration);
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        }

        private static bool IsOverloaded(System.Net.HttpStatusCode statusCode, string responseString)
        {
            if (statusCode == System.Net.HttpStatusCode.ServiceUnavailable) return true;
            if (statusCode == System.Net.HttpStatusCode.NotFound) return true;
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var status = doc.RootElement.GetProperty("error").GetProperty("status").GetString();
                return status is "UNAVAILABLE" or "NOT_FOUND";
            }
            catch { return false; }
        }

        private static string ParseGeminiError(System.Net.HttpStatusCode statusCode, string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var code = doc.RootElement.GetProperty("error").GetProperty("code").GetInt32();
                var status = doc.RootElement.GetProperty("error").GetProperty("status").GetString();
                return (code, status) switch
                {
                    (503, _) or (_, "UNAVAILABLE") => "⚠️ AI đang bị quá tải, vui lòng thử lại sau ít phút.",
                    (429, _) or (_, "RESOURCE_EXHAUSTED") => "⚠️ Đã vượt quá giới hạn yêu cầu AI. Vui lòng thử lại sau.",
                    (404, _) or (_, "NOT_FOUND") => "⚠️ AI đang bị quá tải, vui lòng thử lại sau ít phút.",
                    (400, _) or (_, "INVALID_ARGUMENT") => "⚠️ Yêu cầu không hợp lệ. Vui lòng kiểm tra nội dung gửi lên.",
                    (401, _) or (403, _) or (_, "PERMISSION_DENIED") => "⚠️ API key không hợp lệ hoặc không có quyền truy cập.",
                    _ => $"⚠️ AI tạm thời không khả dụng (lỗi {code}). Vui lòng thử lại sau."
                };
            }
            catch
            {
                return $"⚠️ AI tạm thời không khả dụng ({statusCode}). Vui lòng thử lại sau.";
            }
        }

        private static string ResolveApiKey(IConfiguration configuration)
        {
            var apiKey = configuration["GeminiSettings:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = configuration["GEMINI_API_KEY"];
            return apiKey?.Trim() ?? string.Empty;
        }

        /// <summary>POST to Gemini with retry + fallback. Returns (responseString, isSuccess, lastStatusCode).</summary>
        private async Task<(string body, bool ok, System.Net.HttpStatusCode statusCode)> PostWithRetryAsync(Func<string, object> buildBody)
        {
            string lastBody = string.Empty;
            var lastStatus = System.Net.HttpStatusCode.ServiceUnavailable;

            for (int i = 0; i < _models.Length; i++)
            {
                if (_retryDelaysMs[i] > 0)
                    await Task.Delay(_retryDelaysMs[i]);

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_models[i]}:generateContent?key={_apiKey}";
                var json = JsonSerializer.Serialize(buildBody(_models[i]));
                var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                lastBody = await response.Content.ReadAsStringAsync();
                lastStatus = response.StatusCode;

                if (response.IsSuccessStatusCode) return (lastBody, true, lastStatus);

                // Only retry on overload; other errors (400, 401, 403) fail immediately
                if (!IsOverloaded(response.StatusCode, lastBody))
                    return (lastBody, false, lastStatus);
            }

            return (lastBody, false, lastStatus);
        }

        private static string ExtractText(string responseString)
        {
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "Không có phản hồi từ AI.";
        }

        public Task<string> ChatAsync(string message) =>
            ChatWithHistoryAsync(new List<(string role, string text)> { ("user", message) });

        public async Task<string> ChatWithHistoryAsync(IReadOnlyList<(string role, string text)> turns)
        {
            if (turns == null || turns.Count == 0) return "Không có nội dung để gửi.";
            if (string.IsNullOrWhiteSpace(_apiKey)) return "Tính năng AI chưa được cấu hình. Vui lòng liên hệ quản trị viên.";
            try
            {
                var contents = turns.Select(t => new
                {
                    role = t.role == "user" ? "user" : "model",
                    parts = new[] { new { text = string.IsNullOrEmpty(t.text) ? " " : t.text } }
                }).ToArray();

                var (body, ok, statusCode) = await PostWithRetryAsync(_ => new { contents });
                if (!ok) return ParseGeminiError(statusCode, body);
                return ExtractText(body);
            }
            catch (TaskCanceledException) { return "Yêu cầu đã hết thời gian chờ. Vui lòng kiểm tra kết nối mạng."; }
            catch (Exception ex) { return $"Lỗi: {ex.Message}"; }
        }

        public async Task<string> ChatWithSystemContextAsync(string systemPrompt, IReadOnlyList<(string role, string text)> turns)
        {
            if (turns == null || turns.Count == 0) return "Không có nội dung để gửi.";
            if (string.IsNullOrWhiteSpace(_apiKey)) return "Tính năng AI chưa được cấu hình. Vui lòng liên hệ quản trị viên.";
            try
            {
                var contents = turns.Select(t => new
                {
                    role = t.role == "user" ? "user" : "model",
                    parts = new[] { new { text = string.IsNullOrEmpty(t.text) ? " " : t.text } }
                }).ToArray();

                var (body, ok, statusCode) = await PostWithRetryAsync(_ => new
                {
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                    contents
                });
                if (!ok) return ParseGeminiError(statusCode, body);
                return ExtractText(body);
            }
            catch (Exception ex) { return $"Lỗi: {ex.Message}"; }
        }

        public async Task<string> AnalyzeVideoAsync(string videoUrl, string prompt)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) return "URL video không hợp lệ.";
            try
            {
                // Step 1: Download video
                var videoBytes = await _httpClient.GetByteArrayAsync(videoUrl);
                var mimeType = "video/mp4";
                if (videoUrl.Contains(".mov")) mimeType = "video/quicktime";
                else if (videoUrl.Contains(".avi")) mimeType = "video/x-msvideo";
                else if (videoUrl.Contains(".webm")) mimeType = "video/webm";

                // Step 2: Upload to Gemini File API (upload once, reuse URI across retries)
                var initiateUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?uploadType=resumable&key={_apiKey}";
                var initiateRequest = new HttpRequestMessage(HttpMethod.Post, initiateUrl);
                initiateRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
                initiateRequest.Headers.Add("X-Goog-Upload-Command", "start");
                initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", videoBytes.Length.ToString());
                initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
                initiateRequest.Content = new StringContent(JsonSerializer.Serialize(new { file = new { display_name = "football_video" } }), Encoding.UTF8, "application/json");

                var initiateResp = await _httpClient.SendAsync(initiateRequest);
                if (!initiateResp.IsSuccessStatusCode)
                    return $"Lỗi khởi tạo upload: {await initiateResp.Content.ReadAsStringAsync()}";

                var uploadUri = initiateResp.Headers.TryGetValues("X-Goog-Upload-URL", out var vals) ? vals.FirstOrDefault() : null;
                if (string.IsNullOrEmpty(uploadUri)) return "Không lấy được upload URL từ Gemini.";

                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUri);
                uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
                uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
                uploadRequest.Content = new ByteArrayContent(videoBytes);
                uploadRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                var uploadResp = await _httpClient.SendAsync(uploadRequest);
                var uploadStr = await uploadResp.Content.ReadAsStringAsync();
                if (!uploadResp.IsSuccessStatusCode) return $"Lỗi upload video: {uploadStr}";

                using var uploadDoc = JsonDocument.Parse(uploadStr);
                var fileUri = uploadDoc.RootElement.GetProperty("file").GetProperty("uri").GetString();
                var fileName = uploadDoc.RootElement.GetProperty("file").GetProperty("name").GetString();
                if (string.IsNullOrEmpty(fileUri)) return "Không lấy được URI file từ Gemini.";

                // Step 3: Poll until ACTIVE
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(3000);
                    var statusResp = await _httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={_apiKey}");
                    var statusStr = await statusResp.Content.ReadAsStringAsync();
                    using var statusDoc = JsonDocument.Parse(statusStr);
                    var state = statusDoc.RootElement.GetProperty("state").GetString();
                    if (state == "ACTIVE") break;
                    if (state == "FAILED") return "File xử lý thất bại trên Gemini.";
                }

                // Step 4: Analyze with retry + fallback
                var footballPrompt = string.IsNullOrWhiteSpace(prompt)
                    ? "Hãy phân tích tình huống trong video bóng đá này. Mô tả chi tiết: các cầu thủ liên quan, chiến thuật, kỹ thuật, và nhận xét về tình huống."
                    : prompt;

                var systemInstruction =
                    "Bạn là AI chuyên gia phân tích video bóng đá cho hệ thống VN Player Rating — nền tảng đánh giá cầu thủ bóng đá Việt Nam.\n\n" +

                    "=== PHẠM VI HỖ TRỢ ===\n" +
                    "Hệ thống CHỈ hỗ trợ phân tích video từ 3 giải đấu bóng đá Việt Nam:\n" +
                    "  • V-League 1 (giải VĐQG hạng cao nhất Việt Nam)\n" +
                    "  • V-League 2 (giải hạng Nhất Việt Nam)\n" +
                    "  • Vietnam Cup (Cúp Quốc gia Việt Nam)\n\n" +

                    "=== BƯỚC 1: QUAN SÁT VÀ NHẬN DIỆN ===\n" +
                    "Trước khi làm bất cứ điều gì, hãy xem toàn bộ video và tìm kiếm CÁC DẤU HIỆU SAU để xác định nguồn gốc:\n\n" +

                    "DẤU HIỆU NHẬN BIẾT BÓNG ĐÁ VIỆT NAM (nếu thấy BẤT KỲ dấu hiệu nào → đây là bóng đá Việt Nam):\n" +
                    "  • Bảng tỉ số hiển thị tên CLB Việt Nam viết tắt hoặc đầy đủ:\n" +
                    "    - CAHN (Công An Hà Nội), HAN hoặc HANU (Hà Nội FC), HAGL (Hoàng Anh Gia Lai),\n" +
                    "    - TXNĐ (Thép Xanh Nam Định), SHB (SHB Đà Nẵng), BFC (Becamex Bình Dương),\n" +
                    "    - QNK (Quảng Nam), SLNA (Sông Lam Nghệ An), ĐTV (Đông Á Thanh Hóa),\n" +
                    "    - HCM (TP.HCM FC), LAS (Long An), KKT (Khánh Hòa), PVF, v.v.\n" +
                    "  • Biển quảng cáo xung quanh sân có chữ tiếng Việt hoặc thương hiệu Việt Nam:\n" +
                    "    - Viettel, Vingroup, VinFast, Becamex, Bia Sài Gòn, Bia Hà Nội, Sabeco, Habeco,\n" +
                    "    - VPBank, Techcombank, MB Bank, BIDV, Agribank, VietinBank,\n" +
                    "    - FPT, VTV, VTC, Next Media, Thể Thao TV, v.v.\n" +
                    "  • Logo V-League, V-League 2, hoặc Vietnam Cup xuất hiện trên màn hình\n" +
                    "  • Bình luận viên nói tiếng Việt trong phần âm thanh\n" +
                    "  • Sân vận động quen thuộc của Việt Nam (Mỹ Đình, Hàng Đẫy, Thống Nhất, Quy Nhơn, v.v.)\n" +
                    "  • Màu áo và huy hiệu của các CLB Việt Nam\n" +
                    "  • Chữ tiếng Việt xuất hiện bất kỳ đâu trên màn hình (tên cầu thủ, chú thích, đồ họa)\n\n" +

                    "DẤU HIỆU NHẬN BIẾT GIẢI NGOẠI (chỉ kết luận là giải ngoại khi có DẤU HIỆU RÕ RÀNG, KHÔNG PHỎNG ĐOÁN):\n" +
                    "  • Logo chính thức của giải ngoại xuất hiện rõ ràng: Premier League, La Liga, Bundesliga,\n" +
                    "    Serie A, Ligue 1, Champions League, Europa League, World Cup, Asian Cup, v.v.\n" +
                    "  • Tên CLB nước ngoài nổi tiếng trên bảng tỉ số: Manchester City, Liverpool, Real Madrid,\n" +
                    "    Barcelona, Bayern Munich, PSG, Juventus, Chelsea, Arsenal, v.v.\n" +
                    "  • Bình luận viên nói tiếng nước ngoài (Anh, Tây Ban Nha, Đức, Pháp, v.v.) kết hợp với\n" +
                    "    không có bất kỳ dấu hiệu Việt Nam nào\n" +
                    "  • Biển quảng cáo hoàn toàn bằng tiếng nước ngoài, không có chữ tiếng Việt nào\n\n" +

                    "=== BƯỚC 2: RA QUYẾT ĐỊNH ===\n\n" +

                    "TRƯỜNG HỢP A — Video bóng đá Việt Nam (V-League 1 / V-League 2 / Vietnam Cup):\n" +
                    "→ TIẾN HÀNH PHÂN TÍCH ĐẦY ĐỦ ngay lập tức. Không cần hỏi thêm.\n\n" +

                    "TRƯỜNG HỢP B — Video bóng đá nhưng KHÔNG XÁC ĐỊNH được giải (không thấy dấu hiệu rõ ràng của giải ngoại):\n" +
                    "→ GIẢ ĐỊNH ĐÂY LÀ BÓNG ĐÁ VIỆT NAM và TIẾN HÀNH PHÂN TÍCH.\n" +
                    "→ Ghi chú ngắn ở đầu: 'Không xác định được giải đấu cụ thể từ video, tiến hành phân tích tình huống.'\n\n" +

                    "TRƯỜNG HỢP C — Video bóng đá nhưng CÓ BẰNG CHỨNG RÕ RÀNG là giải nước ngoài:\n" +
                    "→ TỪ CHỐI và trả lời CHÍNH XÁC câu sau (không thêm bớt):\n" +
                    "'Video này thuộc giải đấu nước ngoài. Hệ thống VN Player Rating chỉ hỗ trợ phân tích video từ V-League 1, V-League 2 và Vietnam Cup.'\n\n" +

                    "TRƯỜNG HỢP D — Video KHÔNG PHẢI bóng đá (nấu ăn, âm nhạc, xe cộ, thể thao khác, v.v.):\n" +
                    "→ TỪ CHỐI và trả lời CHÍNH XÁC câu sau (không thêm bớt):\n" +
                    "'Video này không phải nội dung bóng đá. Hệ thống chỉ hỗ trợ phân tích video tình huống từ V-League 1, V-League 2 và Vietnam Cup.'\n\n" +

                    "=== BƯỚC 3: NỘI DUNG PHÂN TÍCH (chỉ khi thuộc Trường hợp A hoặc B) ===\n" +
                    "Phân tích toàn diện và chi tiết theo các mục sau:\n\n" +
                    "1. TỔNG QUAN TÌNH HUỐNG\n" +
                    "   - Mô tả tình huống diễn ra trong video (tấn công, phòng thủ, phản công, cố định, v.v.)\n" +
                    "   - Thời điểm trong trận (nếu thấy được từ bảng tỉ số)\n" +
                    "   - Các cầu thủ liên quan chính\n\n" +
                    "2. PHÂN TÍCH CHIẾN THUẬT\n" +
                    "   - Sơ đồ và cách bố trí đội hình trong tình huống\n" +
                    "   - Ý đồ chiến thuật của đội tấn công\n" +
                    "   - Phản ứng chiến thuật của đội phòng thủ\n" +
                    "   - Điểm mạnh và điểm yếu trong cách triển khai\n\n" +
                    "3. PHÂN TÍCH KỸ THUẬT CÁ NHÂN\n" +
                    "   - Kỹ thuật xử lý bóng của từng cầu thủ liên quan\n" +
                    "   - Chất lượng đường chuyền, dứt điểm, kiểm soát bóng\n" +
                    "   - Di chuyển không bóng và tạo khoảng trống\n\n" +
                    "4. NHẬN XÉT VÀ ĐÁNH GIÁ\n" +
                    "   - Điểm nổi bật đáng khen\n" +
                    "   - Điểm cần cải thiện\n" +
                    "   - Bài học chiến thuật/kỹ thuật rút ra từ tình huống này\n\n" +

                    "=== LƯU Ý QUAN TRỌNG ===\n" +
                    "• KHÔNG BAO GIỜ từ chối phân tích chỉ vì tên file video trông lạ hoặc không có nghĩa (ví dụ: abc123.mp4, xmqt419.mp4). Tên file KHÔNG liên quan đến nội dung.\n" +
                    "• KHÔNG từ chối khi không chắc chắn — hãy luôn ưu tiên phân tích.\n" +
                    "• Chỉ từ chối khi có BẰNG CHỨNG TRỰC QUAN RÕ RÀNG trong video cho thấy đây là giải ngoại hoặc không phải bóng đá.\n" +
                    "• Trả lời bằng tiếng Việt, ngôn ngữ chuyên môn bóng đá, rõ ràng và có cấu trúc.";

                var (body, ok, statusCode) = await PostWithRetryAsync(model => new
                {
                    system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new { file_data = new { mime_type = mimeType, file_uri = fileUri } },
                                new { text = footballPrompt }
                            }
                        }
                    }
                });

                if (!ok) return ParseGeminiError(statusCode, body);
                return ExtractText(body);
            }
            catch (TaskCanceledException) { return "Yêu cầu hết thời gian chờ. Video có thể quá lớn hoặc mạng chậm."; }
            catch (Exception ex) { return $"Lỗi: {ex.Message}"; }
        }
    }
}
