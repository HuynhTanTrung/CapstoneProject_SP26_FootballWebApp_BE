namespace VNFootballLeagues.Services.Services;

public static class SubscriptionCredits
{
    private static readonly Dictionary<string, (int AiVideo, int ForumPost, int AiMatch)> Credits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRIAL"]               = (1,  2,  5),
        ["MONTHLY"]             = (15, 15, 30),
        ["QUARTERLY"]           = (45, 50, 100),
        ["TOPUP_AI_VIDEO"]      = (5,  0,  0),
        ["TOPUP_FORUM_POST"]    = (0,  10, 0),
        ["TOPUP_AI_MATCH"]      = (0,  0,  10),
    };

    public static bool IsTopUp(string planCode) =>
        planCode.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase);

    public static (int AiVideo, int ForumPost, int AiMatch) GetCredits(string planCode)
    {
        return Credits.TryGetValue(planCode, out var c) ? c : (0, 0, 0);
    }
}
