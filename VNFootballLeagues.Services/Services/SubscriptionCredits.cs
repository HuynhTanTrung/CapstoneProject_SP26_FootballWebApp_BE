namespace VNFootballLeagues.Services.Services;

public static class SubscriptionCredits
{
    // Fixed credits — consumed, not daily-reset
    private static readonly Dictionary<string, (int AiVideo, int ForumPost, int AiArticle)> Credits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRIAL"]                = (1,  2,  10),
        ["MONTHLY"]              = (15, 15, 30),
        ["QUARTERLY"]            = (45, 50, 100),
        ["TOPUP_AI_VIDEO"]       = (5,  0,  0),
        ["TOPUP_FORUM_POST"]     = (0,  10, 0),
        ["TOPUP_AI_ARTICLE"]     = (0,  0,  10),
    };

    // Daily AI analysis limits (match + player — web only, not extension)
    private static readonly Dictionary<string, int> DailyAiAnalysisLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FREE"]        = 3,
        ["TRIAL"]       = 10,
        ["MONTHLY"]     = 20,
        ["QUARTERLY"]   = 30,
    };

    public static bool IsTopUp(string planCode) =>
        planCode.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase);

    public static (int AiVideo, int ForumPost, int AiArticle) GetCredits(string planCode)
    {
        return Credits.TryGetValue(planCode, out var c) ? c : (0, 0, 0);
    }

    /// <summary>Returns daily AI analysis limit for web match/player analysis.</summary>
    public static int GetDailyAiAnalysisLimit(string? planCode, bool isActive)
    {
        if (!isActive || planCode == null) return DailyAiAnalysisLimits["FREE"];
        return DailyAiAnalysisLimits.TryGetValue(planCode, out var limit) ? limit : DailyAiAnalysisLimits["FREE"];
    }
}
