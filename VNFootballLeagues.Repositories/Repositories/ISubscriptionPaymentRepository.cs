using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface ISubscriptionPaymentRepository
{
    Task<SubscriptionPayment?> GetByPaymentCodeAsync(string paymentCode);
    Task<SubscriptionPayment?> GetByPaymentCodeForUserAsync(Guid userId, string paymentCode);
    Task<SubscriptionPayment?> GetActivePendingByUserIdAsync(Guid userId);
    Task AddAsync(SubscriptionPayment payment);
    Task UpdateAsync(SubscriptionPayment payment);
}
