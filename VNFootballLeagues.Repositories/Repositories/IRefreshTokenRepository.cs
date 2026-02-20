using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetValidTokenAsync(string token);
    Task RevokeTokenAsync(string token);
    Task RevokeAllUserTokensAsync(Guid userId);
    Task AddAsync(RefreshToken refreshToken);
}
