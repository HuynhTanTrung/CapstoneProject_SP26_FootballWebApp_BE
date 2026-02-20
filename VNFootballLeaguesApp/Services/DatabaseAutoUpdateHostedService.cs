using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeaguesApp.Settings;

namespace VNFootballLeaguesApp.Services;

public class DatabaseAutoUpdateHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseAutoUpdateHostedService> _logger;
    private readonly DatabaseAutoUpdateSettings _settings;

    public DatabaseAutoUpdateHostedService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseAutoUpdateHostedService> logger,
        IOptions<DatabaseAutoUpdateSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Database auto update is disabled.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VNFootballLeaguesDBContext>();

        try
        {
            if (_settings.UseMigrations)
            {
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Database migrations applied successfully.");
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                    _logger.LogInformation("No pending migrations. Database is ready.");
                }
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("Database ensured created successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while auto-updating database at startup.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
