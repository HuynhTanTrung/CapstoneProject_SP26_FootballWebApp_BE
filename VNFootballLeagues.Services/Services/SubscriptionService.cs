using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
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

        // Return existing pending payment if still valid
        var existingPending = await _subscriptionPaymentRepository.GetActivePendingByUserIdAsync(userId);
        if (existingPending != null && string.Equals(existingPending.PlanCode, plan.Code, StringComparison.OrdinalIgnoreCase))
        {
            return new SubscriptionPaymentCreateResult { Success = true, Message = "Existing pending payment returned.", Payment = existingPending };
        }

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

    public async Task UpdatePaymentAsync(SubscriptionPayment payment)
    {
        await _subscriptionPaymentRepository.UpdateAsync(payment);
    }

    public async Task<SubscriptionPayment?> PollPaymentStatusAsync(Guid userId, string paymentCode)
    {
        var payment = await _subscriptionPaymentRepository.GetByPaymentCodeForUserAsync(userId, paymentCode);
        if (payment is null) return null;

        // Already paid — return immediately
        if (payment.Status == SubscriptionPaymentStatuses.Paid) return payment;

        // Expired
        if (payment.ExpiresAt < DateTime.UtcNow)
        {
            payment.Status = "Expired";
            await _subscriptionPaymentRepository.UpdateAsync(payment);
            return payment;
        }

        // No API token configured — can't poll
        if (string.IsNullOrWhiteSpace(_sePaySettings.ApiToken)) return payment;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _sePaySettings.ApiToken);

            // Query SePay transactions filtered by transfer content (payment code)
            var url = $"https://my.sepay.vn/userapi/transactions/list?transaction_content={Uri.EscapeDataString(paymentCode)}&limit=5";
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return payment;

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("transactions", out var transactions)) return payment;

            foreach (var tx in transactions.EnumerateArray())
            {
                // Check transfer type = "in" and amount matches
                var type = tx.TryGetProperty("transaction_type", out var tt) ? tt.GetString() : null;
                // amount_in can be string "10000.00" or number
                long amount = 0;
                if (tx.TryGetProperty("amount_in", out var ai))
                {
                    if (ai.ValueKind == JsonValueKind.String)
                        decimal.TryParse(ai.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ;
                    else if (ai.ValueKind == JsonValueKind.Number)
                        amount = (long)ai.GetDecimal();
                    // parse string
                    if (ai.ValueKind == JsonValueKind.String && decimal.TryParse(ai.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dec))
                        amount = (long)dec;
                }
                var content = tx.TryGetProperty("transaction_content", out var tc) ? tc.GetString() : null;

                if (!string.Equals(type, "in", StringComparison.OrdinalIgnoreCase) && amount <= 0) continue;
                if (amount != payment.Amount) continue;
                if (content == null || !content.Contains(paymentCode, StringComparison.OrdinalIgnoreCase)) continue;

                // Match found — process payment
                var now = DateTime.UtcNow;
                long txId = 0;
                if (tx.TryGetProperty("id", out var id))
                {
                    if (id.ValueKind == JsonValueKind.Number) txId = id.GetInt64();
                    else if (id.ValueKind == JsonValueKind.String) long.TryParse(id.GetString(), out txId);
                }

                payment.Status = SubscriptionPaymentStatuses.Paid;
                payment.SePayTransactionId = txId;
                payment.PaidAt = now;
                payment.UpdatedAt = now;
                await _subscriptionPaymentRepository.UpdateAsync(payment);

                // Update subscription
                var subscription = await _userSubscriptionRepository.GetByUserIdAsync(userId);
                var nextBase = subscription is not null && subscription.ExpiresAt > now ? subscription.ExpiresAt : now;

                if (subscription is null)
                {
                    subscription = new UserSubscription
                    {
                        UserId = userId,
                        PlanCode = payment.PlanCode,
                        PlanName = payment.PlanName,
                        Status = SubscriptionStatuses.Active,
                        StartedAt = now,
                        ExpiresAt = nextBase.AddDays(payment.DurationDays),
                        LastPaymentAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _userSubscriptionRepository.AddAsync(subscription);
                }
                else
                {
                    subscription.PlanCode = payment.PlanCode;
                    subscription.PlanName = payment.PlanName;
                    subscription.Status = SubscriptionStatuses.Active;
                    subscription.ExpiresAt = nextBase.AddDays(payment.DurationDays);
                    subscription.LastPaymentAt = now;
                    subscription.UpdatedAt = now;
                    await _userSubscriptionRepository.UpdateAsync(subscription);
                }

                break;
            }
        }
        catch { /* ignore polling errors, return current status */ }

        return payment;
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
