using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly ISubscriptionPaymentRepository _subscriptionPaymentRepository;
    private readonly SePaySettings _sePaySettings;
    private readonly SubscriptionSettings _subscriptionSettings;

    public SubscriptionService(
        IUserRepository userRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ISubscriptionPaymentRepository subscriptionPaymentRepository,
        IOptions<SePaySettings> sePaySettings,
        IOptions<SubscriptionSettings> subscriptionSettings)
    {
        _userRepository = userRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
        _subscriptionPaymentRepository = subscriptionPaymentRepository;
        _sePaySettings = sePaySettings.Value;
        _subscriptionSettings = subscriptionSettings.Value;
    }

    public IReadOnlyCollection<SubscriptionPlanSettings> GetAvailablePlans()
    {
        return _subscriptionSettings.Plans.AsReadOnly();
    }

    public async Task<UserSubscription?> GetCurrentSubscriptionAsync(Guid userId)
    {
        var subscription = await _userSubscriptionRepository.GetByUserIdAsync(userId);
        if (subscription is null)
        {
            return null;
        }

        if (subscription.ExpiresAt <= DateTime.UtcNow && subscription.Status != SubscriptionStatuses.Expired)
        {
            subscription.Status = SubscriptionStatuses.Expired;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _userSubscriptionRepository.UpdateAsync(subscription);
        }

        return subscription;
    }

    public async Task<SubscriptionPaymentCreateResult> CreatePaymentAsync(Guid userId, string planCode)
    {
        var user = await _userRepository.GetWithRolesAsync(userId);
        if (user is null)
        {
            return new SubscriptionPaymentCreateResult
            {
                Success = false,
                Message = "User not found."
            };
        }

        var plan = _subscriptionSettings.Plans.FirstOrDefault(x =>
            string.Equals(x.Code, planCode, StringComparison.OrdinalIgnoreCase));

        if (plan is null)
        {
            return new SubscriptionPaymentCreateResult
            {
                Success = false,
                Message = "Subscription plan is invalid."
            };
        }

        if (string.IsNullOrWhiteSpace(_sePaySettings.BankCode) ||
            string.IsNullOrWhiteSpace(_sePaySettings.AccountNumber) ||
            string.IsNullOrWhiteSpace(_sePaySettings.AccountName))
        {
            return new SubscriptionPaymentCreateResult
            {
                Success = false,
                Message = "SePay settings are incomplete."
            };
        }

        var now = DateTime.UtcNow;
        var paymentCode = GeneratePaymentCode();
        var qrUrl = BuildQrUrl(
            _sePaySettings.QrBaseUrl,
            _sePaySettings.AccountNumber,
            _sePaySettings.BankCode,
            plan.Price,
            paymentCode);

        var payment = new SubscriptionPayment
        {
            PaymentId = Guid.NewGuid(),
            UserId = userId,
            PlanCode = plan.Code.ToUpperInvariant(),
            PlanName = plan.Name,
            Amount = plan.Price,
            DurationDays = plan.DurationDays,
            PaymentCode = paymentCode,
            Provider = "SePay",
            Status = SubscriptionPaymentStatuses.Pending,
            BankCode = _sePaySettings.BankCode,
            AccountNumber = _sePaySettings.AccountNumber,
            AccountName = _sePaySettings.AccountName,
            TransferContent = paymentCode,
            QrUrl = qrUrl,
            ExpiresAt = now.AddMinutes(_subscriptionSettings.PaymentExpiryMinutes),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _subscriptionPaymentRepository.AddAsync(payment);

        return new SubscriptionPaymentCreateResult
        {
            Success = true,
            Message = "Subscription payment created successfully.",
            Payment = payment
        };
    }

    public Task<SubscriptionPayment?> GetPaymentByCodeAsync(Guid userId, string paymentCode)
    {
        return _subscriptionPaymentRepository.GetByPaymentCodeForUserAsync(userId, paymentCode);
    }

    private static string GeneratePaymentCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);

        var suffix = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            suffix[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return $"SUB{DateTime.UtcNow:yyMMddHHmmss}{new string(suffix)}";
    }

    private static string BuildQrUrl(string qrBaseUrl, string accountNumber, string bankCode, long amount, string paymentCode)
    {
        var baseUrl = string.IsNullOrWhiteSpace(qrBaseUrl) ? "https://qr.sepay.vn/img" : qrBaseUrl;
        return $"{baseUrl}?acc={Uri.EscapeDataString(accountNumber)}&bank={Uri.EscapeDataString(bankCode)}&amount={amount}&des={Uri.EscapeDataString(paymentCode)}";
    }
}
