namespace VNFootballLeaguesApp.DTOs.User;

public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public IList<string> Roles { get; set; } = [];
    public bool IsEmailVerified { get; set; }
}

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
}
