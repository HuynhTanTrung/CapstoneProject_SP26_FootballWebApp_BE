using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailOptions)
    {
        _emailSettings = emailOptions.Value;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.SenderEmail) ||
            string.IsNullOrWhiteSpace(_emailSettings.SenderPassword) ||
            string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
        {
            return;
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;
        email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }

    public Task SendVerificationEmailAsync(User user, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_emailSettings.FrontendBaseUrl)
            ? "http://localhost:5000"
            : _emailSettings.FrontendBaseUrl;

        var verifyLink = $"{baseUrl.TrimEnd('/')}/verify-email?token={token}";
        var body = $@"<p>Xin chào {user.FullName},</p>
                      <p>Vui lòng xác thực email bằng cách bấm vào link dưới đây:</p>
                      <p><a href='{verifyLink}'>{verifyLink}</a></p>
                      <p>Link sẽ hết hạn sau 24 giờ.</p>";

        return SendEmailAsync(user.Email, "Xác thực tài khoản VNFootball", body);
    }

    public Task SendPasswordResetEmailAsync(User user, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_emailSettings.FrontendBaseUrl)
            ? "http://localhost:5000"
            : _emailSettings.FrontendBaseUrl;

        var resetLink = $"{baseUrl.TrimEnd('/')}/reset-password?token={token}";
        var body = $@"<p>Xin chào {user.FullName},</p>
                      <p>Bạn đã yêu cầu đặt lại mật khẩu. Bấm vào link sau:</p>
                      <p><a href='{resetLink}'>{resetLink}</a></p>
                      <p>Link sẽ hết hạn sau 1 giờ.</p>";

        return SendEmailAsync(user.Email, "Đặt lại mật khẩu VNFootball", body);
    }

    public Task SendWelcomeEmailAsync(User user)
    {
        var body = $"<p>Chào mừng {user.FullName} đến với VNFootballLeaguesApp!</p>";
        return SendEmailAsync(user.Email, "Chào mừng bạn đến VNFootball", body);
    }
}
