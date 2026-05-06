using Hangfire;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
using VNFootballLeagues.Services.Settings;
using VNFootballLeaguesApp.Extensions;
using VNFootballLeaguesApp.Hubs;
using VNFootballLeaguesApp.Jobs;
using VNFootballLeaguesApp.Middleware;
using VNFootballLeaguesApp.Services;
using VNFootballLeaguesApp.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VNFootballLeagues API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
        
        // For SignalR, we need to allow credentials, so we can't use AllowAnyOrigin
        // If no origins configured, allow localhost for development
        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => true) // Allow any origin in development
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            return;
        }

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services
    .AddAuthenticationServices(builder.Configuration)
    .AddRepositories(builder.Configuration)
    .AddApplicationServices();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.Configure<DatabaseAutoUpdateSettings>(builder.Configuration.GetSection("DatabaseAutoUpdate"));
builder.Services.AddHostedService<DatabaseAutoUpdateHostedService>();
builder.Services.AddHttpClient<IFootballApiService, FootballApiService>();
builder.Services.AddHttpClient(); // For IHttpClientFactory (ImageProxy)
builder.Services.AddResponseCaching();
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddScoped<IChatConversationService, ChatConversationService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Register SofaScore scraper service
builder.Services.AddScoped<ISofascoreScraperService, SofascoreScraperService>();

// Register SignalR
builder.Services.AddSignalR();

// Register Live Match Polling Service (controlled by config)
if (builder.Configuration.GetValue<bool>("LiveMatchPolling:Enabled"))
    builder.Services.AddHostedService<LiveMatchPollingService>();
builder.Services.AddScoped<ISofascoreHybridService, SofascoreHybridService>();

// Register Hangfire
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer(options =>
{
    options.SchedulePollingInterval = TimeSpan.FromMinutes(1);
});
builder.Services.AddScoped<WeeklySyncJob>();
builder.Services.AddScoped<PredictionSettlementJob>();
builder.Services.AddScoped<MonthlyLeaderboardRewardJob>();
builder.Services.AddScoped<SupportAutoCloseJob>();
builder.Services.AddScoped<NotificationService>();
builder.Services.Configure<SofascoreSettings>(
    builder.Configuration.GetSection("SofascoreSettings"));
builder.Services.AddSingleton(resolver =>
    resolver.GetRequiredService<IOptions<SofascoreSettings>>().Value);

var app = builder.Build();

// Auto-apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<VNFootballLeaguesDBContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Migration failed on startup — continuing anyway");
    }
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

var enableSwagger = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VNFootballLeagues API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("FrontendPolicy");
app.UseResponseCaching();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Hangfire dashboard (chỉ dev) + recurring jobs
app.UseHangfireDashboard("/hangfire");
var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

// Cron: mỗi ngày lúc 2:00 sáng UTC+7 = 19:00 UTC
// Matches + standings
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-matches-standings",
    job => job.SyncMatchesAndStandingsAsync(),
    "0 19 * * *"); // 2:00 sáng UTC+7

// Lineups + match statistics (chạy sau 30 phút)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-lineups-matchstats",
    job => job.SyncLineupsAndMatchStatsAsync(),
    "30 19 * * *");

// Player match stats cho các trận FT trong 7 ngày qua (chạy sau 1 tiếng)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-player-match-stats",
    job => job.SyncPlayerMatchStatsForRecentMatchesAsync(),
    "0 20 * * *");

// Player season stats (chạy sau 2 tiếng)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-player-season-stats",
    job => job.SyncPlayerSeasonStatsAsync(),
    "0 21 * * *");

// Cup tree
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-cuptree",
    job => job.SyncCupTreeAsync(),
    "0 19 * * *");

// Chấm điểm dự đoán tỉ số (mỗi 15 phút)
RecurringJob.AddOrUpdate<PredictionSettlementJob>(
    "prediction-settlement",
    job => job.SettlePendingPredictionsAsync(),
    "*/15 * * * *");

// Thưởng top tháng trước (chạy mỗi ngày lúc 00:10 UTC+7; service sẽ tự bỏ qua nếu đã thưởng).
RecurringJob.AddOrUpdate<MonthlyLeaderboardRewardJob>(
    "monthly-leaderboard-reward",
    job => job.RewardPreviousMonthTopUsersAsync(),
    "10 0 * * *",
    new RecurringJobOptions
    {
        TimeZone = vnTimeZone
    });

// Auto-close support tickets (mỗi 1 phút)
RecurringJob.AddOrUpdate<SupportAutoCloseJob>(
    "support-auto-close",
    job => job.RunAsync(),
    "* * * * *");

// Map SignalR hub
app.MapHub<LiveMatchHub>("/hubs/livematch");

app.Run();
