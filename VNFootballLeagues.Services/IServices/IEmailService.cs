using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendVerificationEmailAsync(User user, string token);
    Task SendPasswordResetEmailAsync(User user, string token);
    Task SendWelcomeEmailAsync(User user);
}
