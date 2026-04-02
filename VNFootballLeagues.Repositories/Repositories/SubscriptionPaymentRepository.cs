using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Basic;
using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public class SubscriptionPaymentRepository : GenericRepository<SubscriptionPayment>, ISubscriptionPaymentRepository
{
    public SubscriptionPaymentRepository(VNFootballLeaguesDBContext context) : base(context)
    {
    }

    public Task<SubscriptionPayment?> GetByPaymentCodeAsync(string paymentCode)
    {
        return _context.SubscriptionPayments.FirstOrDefaultAsync(x => x.PaymentCode == paymentCode);
    }

    public Task<SubscriptionPayment?> GetByPaymentCodeForUserAsync(Guid userId, string paymentCode)
    {
        return _context.SubscriptionPayments.FirstOrDefaultAsync(x => x.UserId == userId && x.PaymentCode == paymentCode);
    }

    public Task<SubscriptionPayment?> GetActivePendingByUserIdAsync(Guid userId)
    {
        return _context.SubscriptionPayments
            .Where(x => x.UserId == userId && x.Status == "Pending" && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(SubscriptionPayment payment)
    {
        await _context.SubscriptionPayments.AddAsync(payment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubscriptionPayment payment)
    {
        _context.SubscriptionPayments.Update(payment);
        await _context.SaveChangesAsync();
    }
}
