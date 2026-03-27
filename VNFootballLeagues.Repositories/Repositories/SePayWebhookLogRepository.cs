using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Basic;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public class SePayWebhookLogRepository : GenericRepository<SePayWebhookLog>, ISePayWebhookLogRepository
{
    public SePayWebhookLogRepository(VNFootballLeaguesDBContext context) : base(context)
    {
    }

    public Task<SePayWebhookLog?> GetBySePayTransactionIdAsync(long sePayTransactionId)
    {
        return _context.SePayWebhookLogs.FirstOrDefaultAsync(x => x.SePayTransactionId == sePayTransactionId);
    }

    public async Task AddAsync(SePayWebhookLog webhookLog)
    {
        await _context.SePayWebhookLogs.AddAsync(webhookLog);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SePayWebhookLog webhookLog)
    {
        _context.SePayWebhookLogs.Update(webhookLog);
        await _context.SaveChangesAsync();
    }
}
