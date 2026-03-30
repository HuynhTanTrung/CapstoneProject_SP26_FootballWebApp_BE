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

    public Task<SubscriptionPayment?> GetByPaymentCodeWithUserAsync(string paymentCode)
    {
        return _context.SubscriptionPayments
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.PaymentCode == paymentCode);
    }

    public async Task<IReadOnlyCollection<SubscriptionPayment>> GetAdminPaymentsAsync(
        string? status,
        string? paymentCode,
        string? keyword,
        int pageNumber,
        int pageSize)
    {
        return await BuildAdminQuery(status, paymentCode, keyword)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountAdminPaymentsAsync(string? status, string? paymentCode, string? keyword)
    {
        return BuildAdminQuery(status, paymentCode, keyword).CountAsync();
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

    private IQueryable<SubscriptionPayment> BuildAdminQuery(string? status, string? paymentCode, string? keyword)
    {
        var query = _context.SubscriptionPayments
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentCode))
        {
            var normalizedPaymentCode = paymentCode.Trim();
            query = query.Where(x => x.PaymentCode.Contains(normalizedPaymentCode));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                x.PaymentCode.Contains(normalizedKeyword) ||
                x.PlanCode.Contains(normalizedKeyword) ||
                x.PlanName.Contains(normalizedKeyword) ||
                x.User.Username.Contains(normalizedKeyword) ||
                x.User.Email.Contains(normalizedKeyword) ||
                x.User.FullName.Contains(normalizedKeyword));
        }

        return query.OrderByDescending(x => x.CreatedAt);
    }
}
