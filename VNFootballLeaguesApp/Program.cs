using Hangfire;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Services;
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
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddScoped<IChatConversationService, ChatConversationService>();

// Register SofaScore scraper service
builder.Services.AddScoped<ISofascoreScraperService, SofascoreScraperService>();

// Register SignalR
builder.Services.AddSignalR();

// Register Live Match Polling Service
builder.Services.AddHostedService<LiveMatchPollingService>();
builder.Services.AddScoped<ISofascoreHybridService, SofascoreHybridService>();

// Register Hangfire
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<WeeklySyncJob>();
builder.Services.AddScoped<PredictionSettlementJob>();


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
app.UseStaticFiles(); // Enable serving static files from wwwroot
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Hangfire dashboard (chỉ dev) + recurring jobs
app.UseHangfireDashboard("/hangfire");

// Cron: thứ 2 lúc 3:00 sáng (UTC+7 = 20:00 UTC Chủ nhật)
// Matches + standings
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-matches-standings",
    job => job.SyncMatchesAndStandingsAsync(),
    "0 20 * * 0"); // Chủ nhật 20:00 UTC = Thứ 2 03:00 UTC+7

// Lineups + match statistics (chạy sau 30 phút)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-lineups-matchstats",
    job => job.SyncLineupsAndMatchStatsAsync(),
    "30 20 * * 0");

// Player match stats cho các trận FT trong 7 ngày qua (chạy sau 1 tiếng)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-player-match-stats",
    job => job.SyncPlayerMatchStatsForRecentMatchesAsync(),
    "0 21 * * 0");

// Player season stats (chạy sau 2 tiếng)
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-player-season-stats",
    job => job.SyncPlayerSeasonStatsAsync(),
    "0 22 * * 0");

// Cup tree
RecurringJob.AddOrUpdate<WeeklySyncJob>(
    "weekly-sync-cuptree",
    job => job.SyncCupTreeAsync(),
    "0 20 * * 0");

// Chấm điểm dự đoán tỉ số (mỗi 15 phút)
RecurringJob.AddOrUpdate<PredictionSettlementJob>(
    "prediction-settlement",
    job => job.SettlePendingPredictionsAsync(),
    "*/15 * * * *");

// Map SignalR hub
app.MapHub<LiveMatchHub>("/hubs/livematch");

app.Run();
