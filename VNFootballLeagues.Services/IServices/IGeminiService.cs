namespace VNFootballLeagues.Services.IServices
{
    public interface IGeminiService
    {
        Task<string> ChatAsync(string message);

        /// <summary>
        /// Gửi hội thoại nhiều lượt tới Gemini. Mỗi phần tử: role "user" hoặc "model", và nội dung text.
        /// </summary>
        Task<string> ChatWithHistoryAsync(IReadOnlyList<(string role, string text)> turns);
    }
}
