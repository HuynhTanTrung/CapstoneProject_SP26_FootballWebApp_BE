using System.ComponentModel.DataAnnotations;

namespace VNFootballLeaguesApp.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
