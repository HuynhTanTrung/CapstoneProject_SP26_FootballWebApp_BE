using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Basic;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public class UserSubscriptionRepository : GenericRepository<UserSubscription>, IUserSubscriptionRepository
{
    public UserSubscriptionRepository(VNFootballLeaguesDBContext context) : base(context)
    {
    }

    public Task<UserSubscription?> GetByUserIdAsync(Guid userId)
    {
        return _context.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task AddAsync(UserSubscription subscription)
    {
        await _context.UserSubscriptions.AddAsync(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserSubscription subscription)
    {
        _context.UserSubscriptions.Update(subscription);
        await _context.SaveChangesAsync();
    }
}
