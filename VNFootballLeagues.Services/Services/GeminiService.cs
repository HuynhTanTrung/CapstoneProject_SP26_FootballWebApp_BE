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

        public GeminiService(IConfiguration configuration)
        {
            _apiKey = configuration["GeminiSettings:ApiKey"] 
                ?? throw new ArgumentNullException("GeminiSettings:ApiKey is not configured");

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public Task<string> ChatAsync(string message) =>
            ChatWithHistoryAsync(new List<(string role, string text)> { ("user", message) });

        public async Task<string> ChatWithHistoryAsync(IReadOnlyList<(string role, string text)> turns)
        {
            if (turns == null || turns.Count == 0)
                return "Không có nội dung để gửi.";

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                var contents = turns.Select(t => new
                {
                    role = t.role == "user" ? "user" : "model",
                    parts = new[] { new { text = string.IsNullOrEmpty(t.text) ? " " : t.text } }
                }).ToArray();

                var requestBody = new { contents };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Lỗi API: {response.StatusCode} - {responseString}";
                }

                using var doc = JsonDocument.Parse(responseString);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "Không có phản hồi từ AI.";
            }
            catch (TaskCanceledException)
            {
                return "Yêu cầu đã hết thời gian chờ (60s). Vui lòng kiểm tra kết nối mạng.";
            }
            catch (HttpRequestException ex)
            {
                return $"Lỗi kết nối: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Lỗi: {ex.Message}";
            }
        }
    }
}
