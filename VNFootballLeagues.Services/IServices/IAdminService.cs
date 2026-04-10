using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices
{
    public interface IAdminService
    {
        Task<object> GetAllUsersAsync();
        Task<object> GetUserByIdAsync(Guid userId);
        Task<object> UpdateUserAsync(Guid userId, User updatedUser);
        Task<object> DeleteUserAsync(Guid userId);
        Task<object> PermanentDeleteUserAsync(Guid userId);
        Task<object> GetUserDashboardStatisticsAsync();
        Task<object> GetMoneyEarnedDashboardAsync();
    }
}
