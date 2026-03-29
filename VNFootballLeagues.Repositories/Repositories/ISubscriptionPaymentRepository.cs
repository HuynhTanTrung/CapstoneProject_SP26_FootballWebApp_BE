using VNFootballLeagues.Repositories.Models;

namespace VNFootballLeagues.Repositories.Repositories;

public interface ISubscriptionPaymentRepository
{
    Task<SubscriptionPayment?> GetByPaymentCodeAsync(string paymentCode);
    Task<SubscriptionPayment?> GetByPaymentCodeForUserAsync(Guid userId, string paymentCode);
    Task<SubscriptionPayment?> GetByPaymentCodeWithUserAsync(string paymentCode);
    Task<IReadOnlyCollection<SubscriptionPayment>> GetAdminPaymentsAsync(
        string? status,
        string? paymentCode,
        string? keyword,
        int pageNumber,
        int pageSize);
    Task<int> CountAdminPaymentsAsync(string? status, string? paymentCode, string? keyword);
    Task AddAsync(SubscriptionPayment payment);
    Task UpdateAsync(SubscriptionPayment payment);
}
