using VNFootballLeagues.Services.Models.Auth;

namespace VNFootballLeagues.Services.IServices;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string email, string password, string fullName);
    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);
    Task<bool> LogoutAsync(string refreshToken);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task<bool> VerifyEmailAsync(string token);
    Task<bool> ForgotPasswordAsync(string email);
    Task<bool> ResetPasswordAsync(string token, string newPassword);
    Task<bool> ResendVerificationEmailAsync(string email);
    Task<AuthResult> GetCurrentUserAsync(Guid userId);
}
