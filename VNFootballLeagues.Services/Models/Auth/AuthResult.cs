using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.Models.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string[] Errors { get; set; } = [];
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public User? User { get; set; }
    public IList<string> Roles { get; set; } = [];

    public static AuthResult Failed(string message, params string[] errors)
        => new() { Success = false, Message = message, Errors = errors };
}
