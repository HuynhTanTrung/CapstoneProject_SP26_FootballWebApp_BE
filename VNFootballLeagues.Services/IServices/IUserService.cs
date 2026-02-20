using System.Security.Claims;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<IList<string>> GetUserRolesAsync(Guid userId);
    Task IncrementFailedLoginAsync(Guid userId);
    Task ResetFailedLoginAsync(Guid userId);
    Task LockoutUserAsync(Guid userId);
    Task<bool> IsLockedOutAsync(User user);
    Task AddToRoleAsync(Guid userId, string roleName);
    Guid? GetUserId(ClaimsPrincipal principal);
}
