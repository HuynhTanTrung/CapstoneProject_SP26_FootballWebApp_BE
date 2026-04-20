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

                var systemInstruction = "Bạn là AI chuyên phân tích video bóng đá cho hệ thống VN Player Rating, tập trung vào 3 giải đấu: V-League 1, V-League 2, Vietnam Cup. " +
                    "Nếu video KHÔNG phải về bóng đá hoặc không liên quan đến các giải bóng đá Việt Nam, hãy từ chối và trả lời: " +
                    "'Video này không phải nội dung bóng đá. Hệ thống chỉ hỗ trợ phân tích video tình huống từ V-League 1, V-League 2 và Vietnam Cup.'";

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
