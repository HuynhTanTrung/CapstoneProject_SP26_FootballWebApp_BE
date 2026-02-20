using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Basic;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public class PasswordResetRepository : GenericRepository<PasswordResetToken>, IPasswordResetRepository
{
    public PasswordResetRepository(VNFootballLeaguesDBContext context) : base(context)
    {
    }

    public Task<PasswordResetToken?> GetValidTokenAsync(string token)
    {
        return _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);
    }

    public async Task MarkUsedAsync(int tokenId)
    {
        await _context.PasswordResetTokens
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsUsed, true));
    }

    public async Task RevokeAllActiveTokensAsync(Guid userId)
    {
        await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsUsed, true));
    }

    public async Task AddAsync(PasswordResetToken token)
    {
        await _context.PasswordResetTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }
}
