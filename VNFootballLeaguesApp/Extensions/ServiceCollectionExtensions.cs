using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeaguesApp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection("JwtSettings"));
        services.Configure<EmailSettings>(config.GetSection("EmailSettings"));

        var jwtSettings = config.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
        var secretKey = string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
            ? "THIS_IS_A_DEVELOPMENT_SECRET_KEY_PLEASE_CHANGE_IT_1234567890"
            : jwtSettings.SecretKey;

        var key = Encoding.UTF8.GetBytes(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
        });

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("loginPolicy", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("forgotPasswordPolicy", opt =>
            {
                opt.PermitLimit = 3;
                opt.Window = TimeSpan.FromHours(1);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("registerPolicy", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromHours(1);
                opt.QueueLimit = 0;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<VNFootballLeaguesDBContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISofascoreScraperService, SofascoreScraperService>();

        return services;
    }
}
