using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface IUserSubscriptionRepository
{
    Task<UserSubscription?> GetByUserIdAsync(Guid userId);
    Task AddAsync(UserSubscription subscription);
    Task UpdateAsync(UserSubscription subscription);
}
