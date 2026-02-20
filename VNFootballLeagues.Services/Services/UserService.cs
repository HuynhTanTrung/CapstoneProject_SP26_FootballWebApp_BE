using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly VNFootballLeaguesDBContext _context;

    public UserService(IUserRepository userRepository, VNFootballLeaguesDBContext context)
    {
        _userRepository = userRepository;
        _context = context;
    }

    public Task<User?> GetByIdAsync(Guid id) => _userRepository.GetWithRolesAsync(id);

    public Task<User?> GetByEmailAsync(string email) => _userRepository.GetWithRolesByEmailAsync(email);

    public Task<User?> GetByUsernameAsync(string username) => _userRepository.GetByUsernameAsync(username);

    public async Task<IList<string>> GetUserRolesAsync(Guid userId)
    {
        var user = await _userRepository.GetWithRolesAsync(userId);
        return user?.UserRoles.Select(ur => ur.Role.RoleName).ToList() ?? [];
    }

    public async Task IncrementFailedLoginAsync(Guid userId)
    {
        var user = await _userRepository.GetWithRolesAsync(userId);
        if (user is null) return;

        user.FailedLoginAttempts += 1;
        if (user.FailedLoginAttempts >= 5)
        {
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public async Task ResetFailedLoginAsync(Guid userId)
    {
        var user = await _userRepository.GetWithRolesAsync(userId);
        if (user is null) return;

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public async Task LockoutUserAsync(Guid userId)
    {
        var user = await _userRepository.GetWithRolesAsync(userId);
        if (user is null) return;

        user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
    }

    public Task<bool> IsLockedOutAsync(User user)
    {
        var locked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow;
        return Task.FromResult(locked);
    }

    public async Task AddToRoleAsync(Guid userId, string roleName)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
        if (role is null)
        {
            return;
        }

        var existed = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.RoleId);
        if (existed)
        {
            return;
        }

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = userId,
            RoleId = role.RoleId,
            AssignedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public Guid? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
