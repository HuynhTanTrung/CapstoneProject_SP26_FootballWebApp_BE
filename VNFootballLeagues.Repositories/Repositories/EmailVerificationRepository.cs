using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Basic;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public class EmailVerificationRepository : GenericRepository<EmailVerificationToken>, IEmailVerificationRepository
{
    public EmailVerificationRepository(VNFootballLeaguesDBContext context) : base(context)
    {
    }

    public Task<EmailVerificationToken?> GetValidTokenAsync(string token)
    {
        return _context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);
    }

    public async Task MarkUsedAsync(int tokenId)
    {
        await _context.EmailVerificationTokens
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsUsed, true));
    }

    public async Task RevokeAllActiveTokensAsync(Guid userId)
    {
        await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && !t.IsUsed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsUsed, true));
    }

    public async Task AddAsync(EmailVerificationToken token)
    {
        await _context.EmailVerificationTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public Task<EmailVerificationToken?> GetLatestValidByUserIdAsync(Guid userId)
    {
        return _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
