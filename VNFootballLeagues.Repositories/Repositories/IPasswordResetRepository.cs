using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface IPasswordResetRepository
{
    Task<PasswordResetToken?> GetValidTokenAsync(string token);
    Task MarkUsedAsync(int tokenId);
    Task RevokeAllActiveTokensAsync(Guid userId);
    Task AddAsync(PasswordResetToken token);
}
