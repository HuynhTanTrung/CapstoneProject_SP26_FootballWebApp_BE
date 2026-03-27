using VNFootballLeagues.Services.Models.Subscriptions;

namespace VNFootballLeagues.Services.IServices;

public interface ISePayWebhookService
{
    Task<SePayWebhookProcessResult> ProcessAsync(SePayWebhookPayload payload, string? authorizationHeader);
}
