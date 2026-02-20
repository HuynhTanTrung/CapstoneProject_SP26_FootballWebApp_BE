using BCrypt.Net;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Auth;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IUserService userService,
        IJwtService jwtService,
        IEmailService emailService,
        IRefreshTokenRepository refreshTokenRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IPasswordResetRepository passwordResetRepository,
        Microsoft.Extensions.Options.IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _userService = userService;
        _jwtService = jwtService;
        _emailService = emailService;
        _refreshTokenRepository = refreshTokenRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _passwordResetRepository = passwordResetRepository;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResult> RegisterAsync(string username, string email, string password, string fullName)
    {
        if (!IsPasswordStrong(password))
        {
            return AuthResult.Failed("Mật khẩu chưa đủ mạnh.",
                "Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
        }

        if (await _userRepository.GetByEmailAsync(email) is not null)
        {
            return AuthResult.Failed("Email đã được sử dụng.");
        }

        if (await _userRepository.GetByUsernameAsync(username) is not null)
        {
            return AuthResult.Failed("Username đã được sử dụng.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            FullName = fullName,
            IsEmailVerified = false,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userService.AddToRoleAsync(user.UserId, "User");

        var verifyToken = new EmailVerificationToken
        {
            Token = _jwtService.GenerateRefreshToken(),
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false
        };

        await _emailVerificationRepository.RevokeAllActiveTokensAsync(user.UserId);
        await _emailVerificationRepository.AddAsync(verifyToken);
        await _emailService.SendVerificationEmailAsync(user, verifyToken.Token);

        var roles = await _userService.GetUserRolesAsync(user.UserId);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            IsRevoked = false
        });

        return new AuthResult
        {
            Success = true,
            Message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực tài khoản.",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = await _userService.GetByIdAsync(user.UserId),
            Roles = roles
        };
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userService.GetByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return AuthResult.Failed("Email hoặc mật khẩu không đúng.");
        }

        if (await _userService.IsLockedOutAsync(user))
        {
            return AuthResult.Failed("Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await _userService.IncrementFailedLoginAsync(user.UserId);
            return AuthResult.Failed("Email hoặc mật khẩu không đúng.");
        }

        if (!user.IsEmailVerified)
        {
            return AuthResult.Failed("Email chưa được xác thực.");
        }

        await _userService.ResetFailedLoginAsync(user.UserId);

        var roles = await _userService.GetUserRolesAsync(user.UserId);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);
        var refreshExpiry = DateTime.UtcNow.AddDays(rememberMe ? 30 : _jwtSettings.RefreshTokenExpiryDays);

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpiry,
            IsRevoked = false
        });

        return new AuthResult
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = user,
            Roles = roles
        };
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        await _refreshTokenRepository.RevokeTokenAsync(refreshToken);
        return true;
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _refreshTokenRepository.GetValidTokenAsync(refreshToken);
        if (storedToken is null)
        {
            return AuthResult.Failed("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        var user = storedToken.User;
        if (!user.IsActive)
        {
            return AuthResult.Failed("Tài khoản không khả dụng.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
        var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

        await _refreshTokenRepository.RevokeTokenAsync(refreshToken);
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            IsRevoked = false
        });

        return new AuthResult
        {
            Success = true,
            Message = "Làm mới token thành công.",
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt,
            User = user,
            Roles = roles
        };
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var verificationToken = await _emailVerificationRepository.GetValidTokenAsync(token);
        if (verificationToken is null)
        {
            return false;
        }

        var user = verificationToken.User;
        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await _emailVerificationRepository.MarkUsedAsync(verificationToken.Id);
        await _emailService.SendWelcomeEmailAsync(user);

        return true;
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var user = await _userService.GetByEmailAsync(email);
        if (user is null)
        {
            return true;
        }

        var resetToken = new PasswordResetToken
        {
            Token = _jwtService.GenerateRefreshToken(),
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsUsed = false
        };

        await _passwordResetRepository.RevokeAllActiveTokensAsync(user.UserId);
        await _passwordResetRepository.AddAsync(resetToken);
        await _emailService.SendPasswordResetEmailAsync(user, resetToken.Token);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        if (!IsPasswordStrong(newPassword))
        {
            return false;
        }

        var resetToken = await _passwordResetRepository.GetValidTokenAsync(token);
        if (resetToken is null)
        {
            return false;
        }

        var user = resetToken.User;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        await _userRepository.UpdateAsync(user);
        await _passwordResetRepository.MarkUsedAsync(resetToken.Id);
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.UserId);
        return true;
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        var user = await _userService.GetByEmailAsync(email);
        if (user is null || user.IsEmailVerified)
        {
            return false;
        }

        var token = new EmailVerificationToken
        {
            Token = _jwtService.GenerateRefreshToken(),
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false
        };

        await _emailVerificationRepository.RevokeAllActiveTokensAsync(user.UserId);
        await _emailVerificationRepository.AddAsync(token);
        await _emailService.SendVerificationEmailAsync(user, token.Token);
        return true;
    }

    public async Task<AuthResult> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userService.GetByIdAsync(userId);
        if (user is null)
        {
            return AuthResult.Failed("Không tìm thấy người dùng.");
        }

        return new AuthResult
        {
            Success = true,
            Message = "Lấy thông tin thành công.",
            User = user,
            Roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList()
        };
    }

    private static bool IsPasswordStrong(string password)
    {
        if (password.Length < 8) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsLower)) return false;
        if (!password.Any(char.IsDigit)) return false;
        return password.Any(ch => !char.IsLetterOrDigit(ch));
    }
}
