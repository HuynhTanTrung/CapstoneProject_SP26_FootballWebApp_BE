namespace VNFootballLeagues.Services.Services;

public static class SubscriptionCredits
{
    private static readonly Dictionary<string, (int AiVideo, int ForumPost)> Credits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRIAL"]             = (1,  2),
        ["MONTHLY"]           = (15, 15),
        ["QUARTERLY"]         = (45, 50),
        ["TOPUP_AI_VIDEO"]    = (5,  0),
        ["TOPUP_FORUM_POST"]  = (0,  10),
    };

    public static bool IsTopUp(string planCode) =>
        planCode.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase);

    public static (int AiVideo, int ForumPost) GetCredits(string planCode)
    {
        return Credits.TryGetValue(planCode, out var c) ? c : (0, 0);
    }
}
