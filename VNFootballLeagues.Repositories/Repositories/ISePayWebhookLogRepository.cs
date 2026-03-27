using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface ISePayWebhookLogRepository
{
    Task<SePayWebhookLog?> GetBySePayTransactionIdAsync(long sePayTransactionId);
    Task AddAsync(SePayWebhookLog webhookLog);
    Task UpdateAsync(SePayWebhookLog webhookLog);
}
