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

        public async Task<object> GetUserDashboardStatisticsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var startOfToday = now.Date;
                var startOfWeek = now.AddDays(-(int)now.DayOfWeek).Date;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var startOfYear = new DateTime(now.Year, 1, 1);

                // User counts
                var totalUsers = await _context.Users.CountAsync();
                var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
                var inactiveUsers = totalUsers - activeUsers;
                var emailVerifiedUsers = await _context.Users.CountAsync(u => u.IsEmailVerified);
                var emailNotVerifiedUsers = totalUsers - emailVerifiedUsers;

                // New users by period
                var newUsersToday = await _context.Users.CountAsync(u => u.CreatedAt >= startOfToday);
                var newUsersThisWeek = await _context.Users.CountAsync(u => u.CreatedAt >= startOfWeek);
                var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= startOfMonth);
                var newUsersThisYear = await _context.Users.CountAsync(u => u.CreatedAt >= startOfYear);

                // Locked/blocked users
                var lockedUsers = await _context.Users.CountAsync(u => u.LockoutEnd > now);
                var usersWithFailedAttempts = await _context.Users.CountAsync(u => u.FailedLoginAttempts > 0);

                // User growth by month (last 6 months)
                var sixMonthsAgo = now.AddMonths(-6);
                var userGrowth = await _context.Users
                    .Where(u => u.CreatedAt >= sixMonthsAgo)
                    .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Count = g.Count()
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                // User role distribution
                var roleDistribution = await _context.UserRoles
                    .GroupBy(ur => ur.Role.RoleName)
                    .Select(g => new
                    {
                        Role = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                // Subscription distribution
                var subscriptionDistribution = await _context.UserSubscriptions
                    .Where(us => us.Status == "active")
                    .GroupBy(us => us.PlanName)
                    .Select(g => new
                    {
                        Plan = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                // Active subscriptions vs expired
                var activeSubscriptions = await _context.UserSubscriptions.CountAsync(us => us.Status == "active" && us.ExpiresAt > now);
                var expiredSubscriptions = await _context.UserSubscriptions.CountAsync(us => us.Status == "expired" || us.ExpiresAt <= now);

                return new
                {
                    status = true,
                    message = "User dashboard statistics retrieved successfully",
                    data = new
                    {
                        summary = new
                        {
                            totalUsers,
                            activeUsers,
                            inactiveUsers,
                            emailVerifiedUsers,
                            emailNotVerifiedUsers,
                            lockedUsers,
                            usersWithFailedAttempts,
                            activeSubscriptions,
                            expiredSubscriptions
                        },
                        newUsers = new
                        {
                            today = newUsersToday,
                            thisWeek = newUsersThisWeek,
                            thisMonth = newUsersThisMonth,
                            thisYear = newUsersThisYear
                        },
                        userGrowth,
                        roleDistribution,
                        subscriptionDistribution
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user dashboard statistics");
                return new { status = false, message = $"Error retrieving user dashboard statistics: {ex.Message}" };
            }
        }

        public async Task<object> GetMoneyEarnedDashboardAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var startOfToday = now.Date;
                var startOfWeek = now.AddDays(-(int)now.DayOfWeek).Date;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var startOfYear = new DateTime(now.Year, 1, 1);

                // Total money earned from successful payments - Use "Paid" status
                var totalEarned = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid")
                    .SumAsync(p => p.Amount);

                // Money earned by period
                var earnedToday = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid" && p.PaidAt >= startOfToday)
                    .SumAsync(p => p.Amount);

                var earnedThisWeek = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid" && p.PaidAt >= startOfWeek)
                    .SumAsync(p => p.Amount);

                var earnedThisMonth = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid" && p.PaidAt >= startOfMonth)
                    .SumAsync(p => p.Amount);

                var earnedThisYear = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid" && p.PaidAt >= startOfYear)
                    .SumAsync(p => p.Amount);

                // Payment statistics
                var totalPayments = await _context.SubscriptionPayments.CountAsync();
                var successfulPayments = await _context.SubscriptionPayments
                    .CountAsync(p => p.Status == "Paid");
                var pendingPayments = await _context.SubscriptionPayments
                    .CountAsync(p => p.Status == "Pending");
                var failedPayments = await _context.SubscriptionPayments
                    .CountAsync(p => p.Status == "Cancelled" || p.Status == "Expired");

                var paymentSuccessRate = successfulPayments > 0
                    ? (double)successfulPayments / totalPayments * 100
                    : 0;

                // Money by plan - Use "Paid" status and PaidAt
                var earningsByPlan = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid")
                    .GroupBy(p => p.PlanName)
                    .Select(g => new
                    {
                        Plan = g.Key,
                        TotalAmount = g.Sum(p => p.Amount),
                        PaymentCount = g.Count()
                    })
                    .OrderByDescending(g => g.TotalAmount)
                    .ToListAsync();

                // Monthly earnings (last 6 months) - Use PaidAt
                var sixMonthsAgo = now.AddMonths(-6);
                var monthlyEarnings = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid" && p.PaidAt >= sixMonthsAgo)
                    .GroupBy(p => new { p.PaidAt.Value.Year, p.PaidAt.Value.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Amount = g.Sum(p => p.Amount),
                        PaymentCount = g.Count()
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                // Earnings by payment provider
                var earningsByProvider = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid")
                    .GroupBy(p => p.Provider)
                    .Select(g => new
                    {
                        Provider = g.Key,
                        TotalAmount = g.Sum(p => p.Amount),
                        PaymentCount = g.Count()
                    })
                    .ToListAsync();

                // Average payment value
                var averagePaymentValue = successfulPayments > 0
                    ? totalEarned / successfulPayments
                    : 0m;

                // Recent successful payments (last 10)
                var recentPayments = await _context.SubscriptionPayments
                    .Where(p => p.Status == "Paid")
                    .OrderByDescending(p => p.PaidAt)
                    .Take(10)
                    .Select(p => new
                    {
                        p.PaymentId,
                        p.PaymentCode,
                        p.UserId,
                        p.PlanName,
                        p.Amount,
                        p.Provider,
                        p.PaidAt
                    })
                    .ToListAsync();

                return new
                {
                    status = true,
                    message = "Money earned dashboard retrieved successfully",
                    data = new
                    {
                        summary = new
                        {
                            totalEarned,
                            totalPayments,
                            successfulPayments,
                            pendingPayments,
                            failedPayments,
                            paymentSuccessRate = Math.Round(paymentSuccessRate, 2),
                            averagePaymentValue = Math.Round(averagePaymentValue, 2)
                        },
                        earningsByPeriod = new
                        {
                            today = earnedToday,
                            thisWeek = earnedThisWeek,
                            thisMonth = earnedThisMonth,
                            thisYear = earnedThisYear
                        },
                        earningsByPlan,
                        monthlyEarnings,
                        earningsByProvider,
                        recentPayments
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting money earned dashboard");
                return new { status = false, message = $"Error retrieving money earned dashboard: {ex.Message}" };
            }
        }
    }
}
