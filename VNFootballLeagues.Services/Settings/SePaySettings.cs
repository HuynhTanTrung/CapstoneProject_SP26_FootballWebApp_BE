namespace VNFootballLeagues.Services.Settings;

public class SePaySettings
{
    public string BankCode { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string QrBaseUrl { get; set; } = "https://qr.sepay.vn/img";

    public string WebhookApiKey { get; set; } = string.Empty;

    /// <summary>SePay User API token from my.sepay.vn → API settings</summary>
    public string ApiToken { get; set; } = string.Empty;
}
