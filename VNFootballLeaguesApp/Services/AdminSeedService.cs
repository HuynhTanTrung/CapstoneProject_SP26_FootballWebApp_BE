using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeaguesApp.Settings;

namespace VNFootballLeaguesApp.Services;

public class AdminSeedService
{
    private readonly VNFootballLeaguesDBContext _context;
    private readonly AdminSeedSettings _settings;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(
        VNFootballLeaguesDBContext context,
        IOptions<AdminSeedSettings> settings,
        ILogger<AdminSeedService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Default admin seed is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Username) ||
            string.IsNullOrWhiteSpace(_settings.Email) ||
            string.IsNullOrWhiteSpace(_settings.Password) ||
            string.IsNullOrWhiteSpace(_settings.FullName))
        {
            _logger.LogWarning("Default admin seed settings are incomplete. Skipping admin seed.");
            return;
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var adminRole = await EnsureRoleAsync(
                    "Admin",
                    "Default administrator role.",
                    cancellationToken);

                var userRole = await EnsureRoleAsync(
                    "User",
                    "Default user role.",
                    cancellationToken);

                var normalizedEmail = _settings.Email.Trim();
                var normalizedUsername = _settings.Username.Trim();

                var userByEmail = await _context.Users
                    .Include(x => x.UserRoles)
                    .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

                var userByUsername = await _context.Users
                    .Include(x => x.UserRoles)
                    .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

                if (userByEmail is not null &&
                    userByUsername is not null &&
                    userByEmail.UserId != userByUsername.UserId)
                {
                    _logger.LogWarning(
                        "Default admin seed found conflicting users for email {Email} and username {Username}. Skipping admin user creation.",
                        normalizedEmail,
                        normalizedUsername);
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var adminUser = userByEmail ?? userByUsername;
                if (adminUser is null)
                {
                    adminUser = new User
                    {
                        UserId = Guid.NewGuid(),
                        Username = normalizedUsername,
                        Email = normalizedEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(_settings.Password.Trim(), workFactor: 12),
                        FullName = _settings.FullName.Trim(),
                        IsEmailVerified = true,
                        IsActive = true,
                        FailedLoginAttempts = 0,
                        LockoutEnd = null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.Users.AddAsync(adminUser, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Default admin account created with email {Email} and username {Username}.",
                        normalizedEmail,
                        normalizedUsername);
                }
                else
                {
                    var hasChanges = false;

                    if (!adminUser.IsActive)
                    {
                        adminUser.IsActive = true;
                        hasChanges = true;
                    }

                    if (!adminUser.IsEmailVerified)
                    {
                        adminUser.IsEmailVerified = true;
                        hasChanges = true;
                    }

                    if (string.IsNullOrWhiteSpace(adminUser.FullName))
                    {
                        adminUser.FullName = _settings.FullName.Trim();
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        adminUser.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    _logger.LogInformation(
                        "Default admin account already exists for email {Email} or username {Username}. Password was left unchanged.",
                        normalizedEmail,
                        normalizedUsername);
                }

                await EnsureUserRoleAsync(adminUser.UserId, adminRole.RoleId, cancellationToken);
                await EnsureUserRoleAsync(adminUser.UserId, userRole.RoleId, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Role> EnsureRoleAsync(string roleName, string description, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(x => x.RoleName == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            RoleName = roleName,
            Description = description
        };

        await _context.Roles.AddAsync(role, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    private async Task EnsureUserRoleAsync(Guid userId, int roleId, CancellationToken cancellationToken)
    {
        var exists = await _context.UserRoles.AnyAsync(
            x => x.UserId == userId && x.RoleId == roleId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
