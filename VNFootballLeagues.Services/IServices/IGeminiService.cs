namespace VNFootballLeagues.Services.IServices
{
    public interface IGeminiService
    {
        Task<string> ChatAsync(string message);
        Task<string> ChatWithHistoryAsync(IReadOnlyList<(string role, string text)> turns);
        Task<string> AnalyzeVideoAsync(string videoUrl, string prompt);
        Task<string> ChatWithSystemContextAsync(string systemPrompt, IReadOnlyList<(string role, string text)> turns);
    }
}
