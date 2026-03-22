namespace VNFootballLeagues.Services.IServices
{
    public interface IGeminiService
    {
        Task<string> ChatAsync(string message);
    }
}
