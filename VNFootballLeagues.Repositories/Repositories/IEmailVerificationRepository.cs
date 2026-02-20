using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface IEmailVerificationRepository
{
    Task<EmailVerificationToken?> GetValidTokenAsync(string token);
    Task MarkUsedAsync(int tokenId);
    Task RevokeAllActiveTokensAsync(Guid userId);
    Task AddAsync(EmailVerificationToken token);
    Task<EmailVerificationToken?> GetLatestValidByUserIdAsync(Guid userId);
}
