using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeagues.Services.Services
{
    public class AdminService : IAdminService
    {
        private readonly VNFootballLeaguesDBContext _context;
        private readonly ILogger<AdminService> _logger;

        public AdminService(VNFootballLeaguesDBContext context, ILogger<AdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<object> GetAllUsersAsync()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.Email,
                        u.FullName,
                        u.IsActive,
                        u.IsEmailVerified,
                        u.CreatedAt,
                        u.UpdatedAt,
                        u.FailedLoginAttempts,
                        u.LockoutEnd,
                        Roles = u.UserRoles.Select(ur => new
                        {
                            ur.Role.RoleId,
                            ur.Role.RoleName,
                            ur.Role.Description
                        }).ToList()
                    })
                    .ToListAsync();

                return new
                {
                    status = true,
                    message = "Users retrieved successfully",
                    data = users,
                    totalCount = users.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return new { status = false, message = $"Error retrieving users: {ex.Message}" };
            }
        }

        public async Task<object> GetUserByIdAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return new { status = false, message = "User not found" };
                }

                return new
                {
                    status = true,
                    message = "User retrieved successfully",
                    data = new
                    {
                        user.UserId,
                        user.Username,
                        user.Email,
                        user.FullName,
                        user.IsActive,
                        user.IsEmailVerified,
                        user.CreatedAt,
                        user.UpdatedAt,
                        user.FailedLoginAttempts,
                        user.LockoutEnd,
                        Roles = user.UserRoles.Select(ur => new
                        {
                            ur.Role.RoleId,
                            ur.Role.RoleName,
                            ur.Role.Description
                        }).ToList()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return new { status = false, message = $"Error retrieving user: {ex.Message}" };
            }
        }
        public async Task<object> UpdateUserAsync(Guid userId, User updatedUser)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return new { status = false, message = "User not found" };
                }

                if (!string.IsNullOrWhiteSpace(updatedUser.Username))
                {
                    var conflict = await _context.Users
                        .FirstOrDefaultAsync(u => u.Username == updatedUser.Username
                                                   && u.UserId != userId
                                                   && u.IsActive);

                    if (conflict != null)
                    {
                        return new { status = false, message = "Username already taken by another active user" };
                    }

                    user.Username = updatedUser.Username;
                }

                if (!string.IsNullOrWhiteSpace(updatedUser.Email))
                {
                    var conflict = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == updatedUser.Email
                                                   && u.UserId != userId
                                                   && u.IsActive);

                    if (conflict != null)
                    {
                        return new { status = false, message = "Email already taken by another active user" };
                    }

                    user.Email = updatedUser.Email;
                }

                if (!string.IsNullOrWhiteSpace(updatedUser.FullName))
                {
                    user.FullName = updatedUser.FullName;
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new
                {
                    status = true,
                    message = "User updated successfully",
                    data = new { user.UserId, user.Username, user.Email, user.FullName, user.IsActive }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", userId);
                return new { status = false, message = $"Error updating user: {ex.Message}" };
            }
        }
        public async Task<object> DeleteUserAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return new { status = false, message = "User not found" };
                }

                if (!user.IsActive)
                {
                    return new { status = false, message = "User is already deleted" };
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new { status = true, message = "User deleted successfully (soft delete)" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", userId);
                return new { status = false, message = $"Error deleting user: {ex.Message}" };
            }
        }

        public async Task<object> PermanentDeleteUserAsync(Guid userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .Include(u => u.RefreshTokens)
                    .Include(u => u.EmailVerificationTokens)
                    .Include(u => u.PasswordResetTokens)
                    .Include(u => u.ChatSessions)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return new { status = false, message = "User not found" };
                }

                _context.UserRoles.RemoveRange(user.UserRoles);
                _context.RefreshTokens.RemoveRange(user.RefreshTokens);
                _context.EmailVerificationTokens.RemoveRange(user.EmailVerificationTokens);
                _context.PasswordResetTokens.RemoveRange(user.PasswordResetTokens);
                _context.ChatSessions.RemoveRange(user.ChatSessions);
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                return new { status = true, message = "User permanently deleted from database" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting user: {UserId}", userId);
                return new { status = false, message = $"Error permanently deleting user: {ex.Message}" };
            }
        }


    }
}
