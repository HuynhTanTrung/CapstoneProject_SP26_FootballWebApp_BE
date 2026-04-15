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

    private static string EmailLayout(string headerColor, string contentHtml) => $@"
<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1.0'></head>
<body style='margin:0;padding:0;background:#f1f5f9;font-family:Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:40px 0;'>
    <tr><td align='center'>
      <table width='560' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;box-shadow:0 4px 24px rgba(0,0,0,0.08);'>
        <tr>
          <td style='background:{headerColor};padding:32px 40px;text-align:center;'>
            <div style='display:inline-block;background:rgba(255,255,255,0.2);border-radius:12px;width:52px;height:52px;line-height:52px;text-align:center;font-size:22px;font-weight:900;color:#fff;margin-bottom:12px;'>VN</div>
            <div style='color:#ffffff;font-size:20px;font-weight:700;'>VN Football Analytics</div>
            <div style='color:rgba(255,255,255,0.85);font-size:12px;margin-top:4px;'>Hệ thống phân tích bóng đá Việt Nam</div>
          </td>
        </tr>
        <tr><td style='padding:40px 40px 32px;'>{contentHtml}</td></tr>
        <tr>
          <td style='background:#f8fafc;padding:20px 40px;border-top:1px solid #e2e8f0;text-align:center;'>
            <div style='color:#94a3b8;font-size:11px;line-height:1.6;'>
              © 2026 VN Football Analytics &nbsp;·&nbsp; FA25SE182<br>
              Email này được gửi tự động, vui lòng không trả lời.
            </div>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";

    public Task SendVerificationEmailAsync(User user, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_emailSettings.FrontendBaseUrl)
            ? "http://localhost:5000"
            : _emailSettings.FrontendBaseUrl;

        var verifyLink = $"{baseUrl.TrimEnd('/')}/verify-email?token={token}";
        var displayName = user.FullName ?? user.Username;

        var content = $@"
<div style='background:#f0fdff;border:1px solid #bae6fd;border-radius:12px;padding:24px;margin-bottom:28px;text-align:center;'>
  <div style='font-size:36px;margin-bottom:8px;'>✉️</div>
  <div style='color:#0284c7;font-size:18px;font-weight:700;margin-bottom:4px;'>Xác thực tài khoản</div>
  <div style='color:#64748b;font-size:13px;'>Chỉ còn một bước nữa thôi!</div>
</div>
<p style='color:#1e293b;font-size:15px;margin:0 0 12px;'>Xin chào <strong>{displayName}</strong>,</p>
<p style='color:#475569;font-size:14px;line-height:1.7;margin:0 0 28px;'>
  Cảm ơn bạn đã đăng ký tài khoản tại <strong style='color:#FF4444;'>VN Football Analytics</strong>.
  Vui lòng xác thực địa chỉ email của bạn để bắt đầu khám phá thống kê bóng đá Việt Nam.
</p>
<div style='text-align:center;margin-bottom:28px;'>
  <a href='{verifyLink}' style='display:inline-block;background:linear-gradient(135deg,#FF4444,#FF6666);color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;padding:14px 36px;border-radius:10px;'>
    ✓ &nbsp; Xác thực email ngay
  </a>
</div>
<div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:14px 16px;margin-bottom:24px;'>
  <div style='color:#94a3b8;font-size:11px;margin-bottom:6px;text-transform:uppercase;letter-spacing:0.5px;'>Hoặc copy link sau vào trình duyệt:</div>
  <div style='color:#0284c7;font-size:11px;word-break:break-all;'>{verifyLink}</div>
</div>
<p style='color:#94a3b8;font-size:12px;margin:0;'>
  ⏱ Link xác thực sẽ hết hạn sau <strong style='color:#64748b;'>24 giờ</strong>.
  Nếu bạn không đăng ký tài khoản này, hãy bỏ qua email này.
</p>";

        return SendEmailAsync(user.Email, "Xác thực tài khoản VNFootball",
            EmailLayout("linear-gradient(135deg,#FF4444 0%,#FF6666 100%)", content));
    }

    public Task SendPasswordResetEmailAsync(User user, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_emailSettings.FrontendBaseUrl)
            ? "http://localhost:5000"
            : _emailSettings.FrontendBaseUrl;

        var resetLink = $"{baseUrl.TrimEnd('/')}/reset-password?token={token}";
        var displayName = user.FullName ?? user.Username;

        var content = $@"
<div style='background:#fff7f7;border:1px solid #fecaca;border-radius:12px;padding:24px;margin-bottom:28px;text-align:center;'>
  <div style='font-size:36px;margin-bottom:8px;'>🔐</div>
  <div style='color:#dc2626;font-size:18px;font-weight:700;margin-bottom:4px;'>Đặt lại mật khẩu</div>
  <div style='color:#64748b;font-size:13px;'>Yêu cầu đặt lại mật khẩu của bạn</div>
</div>
<p style='color:#1e293b;font-size:15px;margin:0 0 12px;'>Xin chào <strong>{displayName}</strong>,</p>
<p style='color:#475569;font-size:14px;line-height:1.7;margin:0 0 28px;'>
  Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.
  Bấm vào nút bên dưới để tạo mật khẩu mới.
</p>
<div style='text-align:center;margin-bottom:28px;'>
  <a href='{resetLink}' style='display:inline-block;background:linear-gradient(135deg,#FF4444,#FF6666);color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;padding:14px 36px;border-radius:10px;'>
    🔑 &nbsp; Đặt lại mật khẩu
  </a>
</div>
<div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:14px 16px;margin-bottom:24px;'>
  <div style='color:#94a3b8;font-size:11px;margin-bottom:6px;text-transform:uppercase;letter-spacing:0.5px;'>Hoặc copy link sau vào trình duyệt:</div>
  <div style='color:#dc2626;font-size:11px;word-break:break-all;'>{resetLink}</div>
</div>
<p style='color:#94a3b8;font-size:12px;margin:0;'>
  ⏱ Link sẽ hết hạn sau <strong style='color:#64748b;'>1 giờ</strong>.
  Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này — tài khoản của bạn vẫn an toàn.
</p>";

        return SendEmailAsync(user.Email, "Đặt lại mật khẩu VNFootball",
            EmailLayout("linear-gradient(135deg,#FF4444 0%,#FF6666 100%)", content));
    }

    public Task SendWelcomeEmailAsync(User user)
    {
        var displayName = user.FullName ?? user.Username;

        var content = $@"
<div style='text-align:center;margin-bottom:28px;'>
  <div style='font-size:48px;margin-bottom:12px;'>🎉</div>
  <h1 style='color:#1e293b;font-size:24px;font-weight:700;margin:0 0 8px;'>Chào mừng bạn!</h1>
  <p style='color:#FF4444;font-size:16px;font-weight:600;margin:0 0 16px;'>{displayName}</p>
  <p style='color:#475569;font-size:14px;line-height:1.7;margin:0;'>
    Tài khoản của bạn đã được xác thực thành công. Bạn đã sẵn sàng khám phá
    toàn bộ dữ liệu bóng đá Việt Nam.
  </p>
</div>
<table width='100%' cellpadding='0' cellspacing='0' style='margin-bottom:8px;'>
  <tr>
    <td width='48%' style='background:#f0fdff;border:1px solid #bae6fd;border-radius:10px;padding:16px;vertical-align:top;'>
      <div style='font-size:22px;margin-bottom:8px;'>⚽</div>
      <div style='color:#0284c7;font-size:13px;font-weight:600;margin-bottom:4px;'>Thống kê cầu thủ</div>
      <div style='color:#64748b;font-size:12px;line-height:1.5;'>Rating, radar chart, lịch sử trận đấu</div>
    </td>
    <td width='4%'></td>
    <td width='48%' style='background:#fff7f7;border:1px solid #fecaca;border-radius:10px;padding:16px;vertical-align:top;'>
      <div style='font-size:22px;margin-bottom:8px;'>🏆</div>
      <div style='color:#dc2626;font-size:13px;font-weight:600;margin-bottom:4px;'>Dự đoán & Thi đấu</div>
      <div style='color:#64748b;font-size:12px;line-height:1.5;'>Tham gia contest, tích điểm thưởng</div>
    </td>
  </tr>
  <tr><td colspan='3' style='height:12px;'></td></tr>
  <tr>
    <td width='48%' style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:16px;vertical-align:top;'>
      <div style='font-size:22px;margin-bottom:8px;'>💬</div>
      <div style='color:#334155;font-size:13px;font-weight:600;margin-bottom:4px;'>Diễn đàn cộng đồng</div>
      <div style='color:#64748b;font-size:12px;line-height:1.5;'>Thảo luận, bình luận trận đấu</div>
    </td>
    <td width='4%'></td>
    <td width='48%' style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:16px;vertical-align:top;'>
      <div style='font-size:22px;margin-bottom:8px;'>🤖</div>
      <div style='color:#334155;font-size:13px;font-weight:600;margin-bottom:4px;'>AI Phân tích</div>
      <div style='color:#64748b;font-size:12px;line-height:1.5;'>Phân tích video & trận đấu bằng AI</div>
    </td>
  </tr>
</table>";

        return SendEmailAsync(user.Email, "Chào mừng bạn đến VNFootball Analytics!",
            EmailLayout("linear-gradient(135deg,#FF4444 0%,#FF6666 100%)", content));
    }
}
