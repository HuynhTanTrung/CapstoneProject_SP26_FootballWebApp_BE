using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Auth;
using VNFootballLeaguesApp.DTOs.Auth;
using VNFootballLeaguesApp.DTOs.Common;
using VNFootballLeaguesApp.DTOs.User;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("registerPolicy")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (dto.Password != dto.ConfirmPassword)
        {
            return BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Mật khẩu xác nhận không khớp.",
                Errors = ["ConfirmPassword does not match."]
            });
        }

        var result = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password, dto.FullName);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("loginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto.Email, dto.Password, dto.RememberMe);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var success = await _authService.LogoutAsync(dto.RefreshToken);
        return Ok(new ApiResponseDto<object>
        {
            Success = success,
            Message = success ? "Đăng xuất thành công." : "Đăng xuất thất bại."
        });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        return ToActionResult(result);
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        var success = await _authService.VerifyEmailAsync(token);
        return Ok(new ApiResponseDto<object>
        {
            Success = success,
            Message = success ? "Xác thực email thành công." : "Token xác thực không hợp lệ hoặc đã hết hạn."
        });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequestDto dto)
    {
        var success = await _authService.ResendVerificationEmailAsync(dto.Email);
        return Ok(new ApiResponseDto<object>
        {
            Success = success,
            Message = success ? "Đã gửi lại email xác thực." : "Không thể gửi lại email xác thực."
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("forgotPasswordPolicy")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        await _authService.ForgotPasswordAsync(dto.Email);
        return Ok(new ApiResponseDto<object>
        {
            Success = true,
            Message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
    {
        if (dto.NewPassword != dto.ConfirmNewPassword)
        {
            return BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Mật khẩu xác nhận không khớp.",
                Errors = ["ConfirmNewPassword does not match."]
            });
        }

        var success = await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
        return Ok(new ApiResponseDto<object>
        {
            Success = success,
            Message = success ? "Đặt lại mật khẩu thành công." : "Token reset không hợp lệ hoặc mật khẩu chưa đủ mạnh."
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Không xác định được người dùng hiện tại."
            });
        }

        var result = await _authService.GetCurrentUserAsync(userId.Value);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(AuthResult result)
    {
        if (!result.Success)
        {
            return BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = result.Message,
                Errors = result.Errors
            });
        }

        if (result.User is null)
        {
            return Ok(new ApiResponseDto<object>
            {
                Success = true,
                Message = result.Message
            });
        }

        var userProfile = new UserProfileDto
        {
            UserId = result.User.UserId,
            Username = result.User.Username,
            Email = result.User.Email,
            FullName = result.User.FullName,
            IsEmailVerified = result.User.IsEmailVerified,
            Roles = result.Roles
        };

        if (!string.IsNullOrWhiteSpace(result.AccessToken))
        {
            return Ok(new ApiResponseDto<AuthResponseDto>
            {
                Success = true,
                Message = result.Message,
                Data = new AuthResponseDto
                {
                    AccessToken = result.AccessToken!,
                    RefreshToken = result.RefreshToken ?? string.Empty,
                    ExpiresAt = result.ExpiresAt ?? DateTime.UtcNow,
                    User = userProfile
                }
            });
        }

        return Ok(new ApiResponseDto<UserProfileDto>
        {
            Success = true,
            Message = result.Message,
            Data = userProfile
        });
    }
}
