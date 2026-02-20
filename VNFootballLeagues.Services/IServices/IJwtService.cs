using System.Security.Claims;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Services.IServices;

public interface IJwtService
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    Guid? GetUserIdFromToken(string token);
}
