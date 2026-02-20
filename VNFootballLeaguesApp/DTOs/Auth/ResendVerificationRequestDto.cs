using System.ComponentModel.DataAnnotations;

namespace VNFootballLeaguesApp.DTOs.Auth;

public class ResendVerificationRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
