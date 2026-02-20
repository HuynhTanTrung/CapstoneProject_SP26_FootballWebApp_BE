using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetWithRolesAsync(Guid userId);
    Task<User?> GetWithRolesByEmailAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
