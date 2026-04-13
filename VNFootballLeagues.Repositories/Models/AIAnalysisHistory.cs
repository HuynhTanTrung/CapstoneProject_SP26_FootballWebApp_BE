#nullable disable
using System;

namespace VNFootballLeagues.Repositories.Models;

/// <summary>Lưu lịch sử phân tích AI theo từng user.</summary>
public class AIAnalysisHistory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>player-rating hoặc match</summary>
    public string AnalysisType { get; set; }

    public int MatchId { get; set; }
    public int? PlayerId { get; set; }

    /// <summary>Kết quả phân tích tiếng Việt từ Gemini</summary>
    public string AnalysisVi { get; set; }

    /// <summary>JSON context data đã dùng để phân tích</summary>
    public string ContextJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; }
}
