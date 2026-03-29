using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly VNFootballLeaguesDBContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly ISubscriptionPaymentRepository _subscriptionPaymentRepository;
    private readonly ISubscriptionPaymentNotificationService _subscriptionPaymentNotificationService;
    private readonly SePaySettings _sePaySettings;
    private readonly SubscriptionSettings _subscriptionSettings;

    public SubscriptionService(
        VNFootballLeaguesDBContext context,
        IUserRepository userRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ISubscriptionPaymentRepository subscriptionPaymentRepository,
        ISubscriptionPaymentNotificationService subscriptionPaymentNotificationService,
        IOptions<SePaySettings> sePaySettings,
        IOptions<SubscriptionSettings> subscriptionSettings)
    {
        _context = context;
        _userRepository = userRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
        _subscriptionPaymentRepository = subscriptionPaymentRepository;
        _subscriptionPaymentNotificationService = subscriptionPaymentNotificationService;
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

    public async Task<AdminSubscriptionPaymentListResult> GetPaymentsForAdminAsync(
        string? status,
        string? paymentCode,
        string? keyword,
        int pageNumber,
        int pageSize)
    {
        var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var totalCount = await _subscriptionPaymentRepository.CountAdminPaymentsAsync(status, paymentCode, keyword);
        var payments = await _subscriptionPaymentRepository.GetAdminPaymentsAsync(
            status,
            paymentCode,
            keyword,
            normalizedPageNumber,
            normalizedPageSize);

        return new AdminSubscriptionPaymentListResult
        {
            Payments = payments,
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize)
        };
    }

    public Task<SubscriptionPayment?> GetPaymentByCodeForAdminAsync(string paymentCode)
    {
        return _subscriptionPaymentRepository.GetByPaymentCodeWithUserAsync(paymentCode.Trim());
    }

    public async Task<AdminSubscriptionPaymentUpdateResult> ManuallyUpdatePaymentStatusAsync(
        Guid adminUserId,
        string paymentCode,
        string status,
        string? reason,
        DateTime? paidAt,
        string? referenceCode,
        string? gateway)
    {
        var normalizedPaymentCode = paymentCode?.Trim() ?? string.Empty;
        var normalizedStatus = status?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedPaymentCode))
        {
            return CreateAdminUpdateResult(false, HttpStatusCode.BadRequest, "Payment code is required.");
        }

        if (!string.Equals(normalizedStatus, SubscriptionPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            return CreateAdminUpdateResult(
                false,
                HttpStatusCode.BadRequest,
                "Only manual update to Paid status is supported.");
        }

        var adminUser = await _userRepository.GetWithRolesAsync(adminUserId);
        if (adminUser is null)
        {
            return CreateAdminUpdateResult(false, HttpStatusCode.NotFound, "Admin user was not found.");
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var payment = await _subscriptionPaymentRepository.GetByPaymentCodeWithUserAsync(normalizedPaymentCode);
            if (payment is null)
            {
                return CreateAdminUpdateResult(false, HttpStatusCode.NotFound, "Subscription payment was not found.");
            }

            if (string.Equals(payment.Status, SubscriptionPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                return CreateAdminUpdateResult(false, HttpStatusCode.BadRequest, "Subscription payment is already marked as paid.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;
                var effectivePaidAt = paidAt ?? now;

                payment.Status = SubscriptionPaymentStatuses.Paid;
                payment.PaidAt = effectivePaidAt;
                payment.SePayReferenceCode = string.IsNullOrWhiteSpace(referenceCode)
                    ? payment.SePayReferenceCode
                    : referenceCode.Trim();
                payment.Gateway = string.IsNullOrWhiteSpace(gateway)
                    ? "AdminManual"
                    : gateway.Trim();
                payment.ManualUpdatedByUserId = adminUserId;
                payment.ManualUpdatedByName = string.IsNullOrWhiteSpace(adminUser.FullName)
                    ? adminUser.Username
                    : adminUser.FullName.Trim();
                payment.ManualUpdatedAt = now;
                payment.ManualUpdateReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                payment.UpdatedAt = now;
                await _subscriptionPaymentRepository.UpdateAsync(payment);

                await UpsertUserSubscriptionAsync(payment, effectivePaidAt, now);

                await transaction.CommitAsync();

                await _subscriptionPaymentNotificationService.PublishAsync(new SubscriptionPaymentNotificationEvent
                {
                    EventName = "payment.succeeded",
                    Message = "Subscription payment was marked as paid manually by admin.",
                    OccurredAt = now,
                    Payment = payment
                });

                var refreshedPayment = await _subscriptionPaymentRepository.GetByPaymentCodeWithUserAsync(normalizedPaymentCode) ?? payment;
                return CreateAdminUpdateResult(
                    true,
                    HttpStatusCode.OK,
                    "Subscription payment updated successfully.",
                    refreshedPayment);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
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

    private async Task UpsertUserSubscriptionAsync(SubscriptionPayment payment, DateTime effectivePaidAt, DateTime now)
    {
        var subscription = await _userSubscriptionRepository.GetByUserIdAsync(payment.UserId);
        var nextExpiryBase = subscription is not null && subscription.ExpiresAt > effectivePaidAt
            ? subscription.ExpiresAt
            : effectivePaidAt;

        if (subscription is null)
        {
            subscription = new UserSubscription
            {
                UserId = payment.UserId,
                PlanCode = payment.PlanCode,
                PlanName = payment.PlanName,
                Status = SubscriptionStatuses.Active,
                StartedAt = effectivePaidAt,
                ExpiresAt = nextExpiryBase.AddDays(payment.DurationDays),
                LastPaymentAt = effectivePaidAt,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _userSubscriptionRepository.AddAsync(subscription);
            return;
        }

        if (subscription.ExpiresAt <= effectivePaidAt)
        {
            subscription.StartedAt = effectivePaidAt;
        }

        subscription.PlanCode = payment.PlanCode;
        subscription.PlanName = payment.PlanName;
        subscription.Status = SubscriptionStatuses.Active;
        subscription.ExpiresAt = nextExpiryBase.AddDays(payment.DurationDays);
        subscription.LastPaymentAt = effectivePaidAt;
        subscription.UpdatedAt = now;
        await _userSubscriptionRepository.UpdateAsync(subscription);
    }

    private static AdminSubscriptionPaymentUpdateResult CreateAdminUpdateResult(
        bool success,
        HttpStatusCode statusCode,
        string message,
        SubscriptionPayment? payment = null)
    {
        return new AdminSubscriptionPaymentUpdateResult
        {
            Success = success,
            StatusCode = (int)statusCode,
            Message = message,
            Payment = payment
        };
    }
}
