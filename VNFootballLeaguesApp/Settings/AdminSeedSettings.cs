namespace VNFootballLeaguesApp.Settings;

public class AdminSeedSettings
{
    public bool Enabled { get; set; } = true;

    public string Username { get; set; } = "admin";

    public string Email { get; set; } = "admin@vnfootball.local";

    public string Password { get; set; } = "Admin@123456";

    public string FullName { get; set; } = "System Administrator";
}
