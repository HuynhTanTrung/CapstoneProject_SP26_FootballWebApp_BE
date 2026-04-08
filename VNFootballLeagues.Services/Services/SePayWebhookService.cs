using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Repositories.Repositories;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;
using VNFootballLeagues.Services.Settings;

namespace VNFootballLeagues.Services.Services;

public class SePayWebhookService : ISePayWebhookService
{
    private readonly VNFootballLeaguesDBContext _context;
    private readonly ISePayWebhookLogRepository _sePayWebhookLogRepository;
    private readonly ISubscriptionPaymentRepository _subscriptionPaymentRepository;
    private readonly ISubscriptionPaymentNotificationService _subscriptionPaymentNotificationService;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly SePaySettings _sePaySettings;

    public SePayWebhookService(
        VNFootballLeaguesDBContext context,
        ISePayWebhookLogRepository sePayWebhookLogRepository,
        ISubscriptionPaymentRepository subscriptionPaymentRepository,
        ISubscriptionPaymentNotificationService subscriptionPaymentNotificationService,
        IUserSubscriptionRepository userSubscriptionRepository,
        IOptions<SePaySettings> sePaySettings)
    {
        _context = context;
        _sePayWebhookLogRepository = sePayWebhookLogRepository;
        _subscriptionPaymentRepository = subscriptionPaymentRepository;
        _subscriptionPaymentNotificationService = subscriptionPaymentNotificationService;
        _userSubscriptionRepository = userSubscriptionRepository;
        _sePaySettings = sePaySettings.Value;
    }

    public async Task<SePayWebhookProcessResult> ProcessAsync(SePayWebhookPayload payload, string? authorizationHeader)
    {
        if (!IsAuthorized(authorizationHeader))
        {
            return CreateResult(false, (int)HttpStatusCode.Unauthorized, "Webhook authorization is invalid.");
        }

        if (payload.Id <= 0)
        {
            return CreateResult(false, (int)HttpStatusCode.BadRequest, "Webhook payload is invalid.");
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var existingLog = await _sePayWebhookLogRepository.GetBySePayTransactionIdAsync(payload.Id);
            if (existingLog is not null)
            {
                return CreateResult(true, (int)HttpStatusCode.OK, "Webhook transaction already processed.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;
                var webhookLog = new SePayWebhookLog
                {
                    WebhookLogId = Guid.NewGuid(),
                    SePayTransactionId = payload.Id,
                    PaymentCode = payload.Code?.Trim(),
                    ReferenceCode = payload.ReferenceCode?.Trim(),
                    TransferType = payload.TransferType?.Trim() ?? string.Empty,
                    TransferAmount = payload.TransferAmount,
                    PayloadJson = JsonSerializer.Serialize(payload),
                    ProcessingStatus = SePayWebhookProcessingStatuses.Received,
                    ReceivedAt = now
                };

                await _sePayWebhookLogRepository.AddAsync(webhookLog);

                var transactionType = payload.TransferType?.Trim();
                if (!string.Equals(transactionType, "in", StringComparison.OrdinalIgnoreCase))
                {
                    await IgnoreWebhookAsync(webhookLog, "Ignoring money-out transaction.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                if (string.IsNullOrWhiteSpace(payload.Code))
                {
                    await IgnoreWebhookAsync(webhookLog, "Payment code was not found in transfer content.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                if (!string.IsNullOrWhiteSpace(_sePaySettings.AccountNumber) &&
                    !string.Equals(_sePaySettings.AccountNumber, payload.AccountNumber?.Trim(), StringComparison.Ordinal))
                {
                    await IgnoreWebhookAsync(webhookLog, "Webhook account number does not match configured SePay account.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                var payment = await _subscriptionPaymentRepository.GetByPaymentCodeAsync(payload.Code.Trim());
                if (payment is null)
                {
                    await IgnoreWebhookAsync(webhookLog, "No pending subscription payment matched this payment code.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                if (payment.Status == SubscriptionPaymentStatuses.Paid)
                {
                    await IgnoreWebhookAsync(webhookLog, "Subscription payment was already marked as paid.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                if (payment.Amount != payload.TransferAmount)
                {
                    await IgnoreWebhookAsync(webhookLog, "Transfer amount does not match expected subscription amount.");
                    await transaction.CommitAsync();
                    return CreateResult(true, (int)HttpStatusCode.OK, "Webhook ignored.");
                }

                var effectivePaidAt = TryParseTransactionDate(payload.TransactionDate) ?? now;

                payment.Status = SubscriptionPaymentStatuses.Paid;
                payment.SePayTransactionId = payload.Id;
                payment.SePayReferenceCode = payload.ReferenceCode?.Trim();
                payment.Gateway = payload.Gateway?.Trim();
                payment.SePayTransactionDate = TryParseTransactionDate(payload.TransactionDate);
                payment.PaidAt = effectivePaidAt;
                payment.UpdatedAt = now;
                await _subscriptionPaymentRepository.UpdateAsync(payment);

                var subscription = await _userSubscriptionRepository.GetByUserIdAsync(payment.UserId);
                var nextExpiryBase = subscription is not null && subscription.ExpiresAt > effectivePaidAt
                    ? subscription.ExpiresAt
                    : effectivePaidAt;

                if (subscription is null)
                {
                    var credits = SubscriptionCredits.GetCredits(payment.PlanCode);
                    subscription = new UserSubscription
                    {
                        UserId = payment.UserId,
                        PlanCode = payment.PlanCode,
                        PlanName = payment.PlanName,
                        Status = SubscriptionStatuses.Active,
                        StartedAt = effectivePaidAt,
                        ExpiresAt = nextExpiryBase.AddDays(payment.DurationDays),
                        LastPaymentAt = effectivePaidAt,
                        AiVideoCreditsRemaining = credits.AiVideo,
                        ForumPostCreditsRemaining = credits.ForumPost,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await _userSubscriptionRepository.AddAsync(subscription);
                }
                else
                {
                    if (subscription.ExpiresAt <= effectivePaidAt)
                    {
                        subscription.StartedAt = effectivePaidAt;
                    }

                    var credits = SubscriptionCredits.GetCredits(payment.PlanCode);
                    subscription.PlanCode = payment.PlanCode;
                    subscription.PlanName = payment.PlanName;
                    subscription.Status = SubscriptionStatuses.Active;
                    subscription.ExpiresAt = nextExpiryBase.AddDays(payment.DurationDays);
                    subscription.LastPaymentAt = effectivePaidAt;
                    subscription.AiVideoCreditsRemaining = credits.AiVideo;
                    subscription.ForumPostCreditsRemaining = credits.ForumPost;
                    subscription.UpdatedAt = now;
                    await _userSubscriptionRepository.UpdateAsync(subscription);
                }

                webhookLog.ProcessingStatus = SePayWebhookProcessingStatuses.Processed;
                webhookLog.ProcessingMessage = "Subscription payment processed successfully.";
                webhookLog.ProcessedAt = now;
                await _sePayWebhookLogRepository.UpdateAsync(webhookLog);

                await transaction.CommitAsync();
                await _subscriptionPaymentNotificationService.PublishAsync(new SubscriptionPaymentNotificationEvent
                {
                    EventName = "payment.succeeded",
                    Message = "Subscription payment processed successfully.",
                    OccurredAt = now,
                    Payment = payment
                });

                return CreateResult(true, (int)HttpStatusCode.OK, "Webhook processed successfully.");
            }
            catch (DbUpdateException ex) when (IsDuplicateWebhookTransaction(ex))
            {
                await transaction.RollbackAsync();
                return CreateResult(true, (int)HttpStatusCode.OK, "Webhook transaction already processed.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private async Task IgnoreWebhookAsync(SePayWebhookLog webhookLog, string message)
    {
        webhookLog.ProcessingStatus = SePayWebhookProcessingStatuses.Ignored;
        webhookLog.ProcessingMessage = message;
        webhookLog.ProcessedAt = DateTime.UtcNow;
        await _sePayWebhookLogRepository.UpdateAsync(webhookLog);
    }

    private bool IsAuthorized(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(_sePaySettings.WebhookApiKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
        {
            return false;
        }

        return string.Equals(headerValue.Scheme, "Apikey", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(headerValue.Parameter, _sePaySettings.WebhookApiKey, StringComparison.Ordinal);
    }

    private static DateTime? TryParseTransactionDate(string? transactionDate)
    {
        if (string.IsNullOrWhiteSpace(transactionDate))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                transactionDate,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(transactionDate, out parsed) ? parsed : null;
    }

    private static bool IsDuplicateWebhookTransaction(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains(
                   "IX_SePayWebhookLog_SePayTransactionId",
                   StringComparison.OrdinalIgnoreCase) == true;
    }

    private static SePayWebhookProcessResult CreateResult(bool success, int statusCode, string message)
    {
        return new SePayWebhookProcessResult
        {
            Success = success,
            StatusCode = statusCode,
            Message = message
        };
    }
}
