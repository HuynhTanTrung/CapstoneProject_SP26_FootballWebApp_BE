using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;
using VNFootballLeagues.Services.Settings;
using VNFootballLeaguesApp.DTOs.Common;
using VNFootballLeaguesApp.DTOs.Subscription;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISubscriptionPaymentNotificationService _subscriptionPaymentNotificationService;
    private readonly IUserService _userService;
    private readonly VNFootballLeaguesDBContext _context;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ISubscriptionPaymentNotificationService subscriptionPaymentNotificationService,
        IUserService userService,
        VNFootballLeaguesDBContext context)
    {
        _subscriptionService = subscriptionService;
        _subscriptionPaymentNotificationService = subscriptionPaymentNotificationService;
        _userService = userService;
        _context = context;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public IActionResult GetPlans()
    {
        var plans = _subscriptionService.GetAvailablePlans()
            .Select(MapPlan)
            .ToList();

        return Ok(new ApiResponseDto<List<SubscriptionPlanDto>>
        {
            Success = true,
            Message = "Subscription plans fetched successfully.",
            Data = plans
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Current user could not be resolved."
            });
        }

        var subscription = await _subscriptionService.GetCurrentSubscriptionAsync(userId.Value);
        var dto = MapSubscription(subscription);

        return Ok(new ApiResponseDto<UserSubscriptionDto>
        {
            Success = true,
            Message = subscription is null
                ? "User does not have an active subscription."
                : "Subscription fetched successfully.",
            Data = dto
        });
    }

    [HttpPost("payments")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreateSubscriptionPaymentRequestDto dto)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Current user could not be resolved."
            });
        }

        var result = await _subscriptionService.CreatePaymentAsync(userId.Value, dto.PlanCode);
        if (!result.Success || result.Payment is null)
        {
            return BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = result.Message,
                Errors = result.Errors
            });
        }

        return Ok(new ApiResponseDto<SubscriptionPaymentDto>
        {
            Success = true,
            Message = result.Message,
            Data = MapPayment(result.Payment)
        });
    }

    [HttpGet("payments/my")]
    [Authorize]
    public async Task<IActionResult> GetMyPayments()
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var payments = await _context.SubscriptionPayments
            .Where(p => p.UserId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();
        return Ok(new ApiResponseDto<object> { Success = true, Data = payments.Select(MapPayment) });
    }

    [HttpGet("payments/{paymentCode}")]
    [Authorize]
    public async Task<IActionResult> GetPayment(string paymentCode)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Current user could not be resolved."
            });
        }

        var payment = await _subscriptionService.GetPaymentByCodeAsync(userId.Value, paymentCode);
        if (payment is null)
        {
            return NotFound(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Subscription payment was not found."
            });
        }

        return Ok(new ApiResponseDto<SubscriptionPaymentDto>
        {
            Success = true,
            Message = "Subscription payment fetched successfully.",
            Data = MapPayment(payment)
        });
    }

    [HttpPost("payments/{paymentCode}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelPayment(string paymentCode)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null) return Unauthorized();
        var payment = await _subscriptionService.GetPaymentByCodeAsync(userId.Value, paymentCode);
        if (payment is null) return NotFound();
        if (payment.Status != "Pending") return BadRequest(new { message = "Only pending payments can be cancelled." });
        payment.Status = "Cancelled";
        payment.UpdatedAt = DateTime.UtcNow;
        await _subscriptionService.UpdatePaymentAsync(payment);
        return Ok(new { success = true, message = "Payment cancelled." });
    }

    /// <summary>Poll SePay API to check if payment has been received. Call every 5s from FE.</summary>
    [HttpPost("payments/{paymentCode}/poll")]
    [Authorize]
    public async Task<IActionResult> PollPayment(string paymentCode)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
            return Unauthorized(new ApiResponseDto<object> { Success = false, Message = "Current user could not be resolved." });

        var payment = await _subscriptionService.PollPaymentStatusAsync(userId.Value, paymentCode);
        if (payment is null)
            return NotFound(new ApiResponseDto<object> { Success = false, Message = "Subscription payment was not found." });

        return Ok(new ApiResponseDto<SubscriptionPaymentDto>
        {
            Success = true,
            Message = payment.Status == "Paid" ? "Payment confirmed." : "Payment pending.",
            Data = MapPayment(payment)
        });
    }

    [HttpGet("payments/{paymentCode}/events")]
    [Authorize]
    public async Task StreamPaymentEvents(string paymentCode)
    {
        var userId = _userService.GetUserId(User);
        if (userId is null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Current user could not be resolved."
            });
            return;
        }

        var payment = await _subscriptionService.GetPaymentByCodeAsync(userId.Value, paymentCode);
        if (payment is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Subscription payment was not found."
            });
            return;
        }

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");

        await using var subscription = _subscriptionPaymentNotificationService.Subscribe(payment.PaymentCode);
        var cancellationToken = HttpContext.RequestAborted;
        var initialEventName = string.Equals(payment.Status, SubscriptionPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
            ? "payment.succeeded"
            : "payment.snapshot";
        var initialMessage = string.Equals(initialEventName, "payment.succeeded", StringComparison.Ordinal)
            ? "Subscription payment was already completed."
            : "Subscription payment stream connected.";

        await WriteSseEventAsync(
            initialEventName,
            CreatePaymentEventDto(initialEventName, initialMessage, payment),
            cancellationToken);

        if (IsTerminalPaymentStatus(payment.Status))
        {
            return;
        }

        using var keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readTask = subscription.Reader.ReadAsync(cancellationToken).AsTask();
                var keepAliveTask = keepAliveTimer.WaitForNextTickAsync(cancellationToken).AsTask();
                var completedTask = await Task.WhenAny(readTask, keepAliveTask);

                if (completedTask == keepAliveTask)
                {
                    if (!await keepAliveTask)
                    {
                        break;
                    }

                    await WriteKeepAliveAsync(cancellationToken);
                    continue;
                }

                var notification = await readTask;
                await WriteSseEventAsync(
                    notification.EventName,
                    CreatePaymentEventDto(
                        notification.EventName,
                        notification.Message,
                        notification.Payment,
                        notification.OccurredAt),
                    cancellationToken);

                if (IsTerminalPaymentStatus(notification.Payment.Status))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static SubscriptionPlanDto MapPlan(SubscriptionPlanSettings plan)
    {
        return new SubscriptionPlanDto
        {
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            DurationDays = plan.DurationDays
        };
    }

    private static UserSubscriptionDto MapSubscription(UserSubscription? subscription)
    {
        if (subscription is null)
        {
            return new UserSubscriptionDto
            {
                Status = SubscriptionStatuses.Inactive,
                IsActive = false
            };
        }

        return new UserSubscriptionDto
        {
            Status = subscription.Status,
            IsActive = subscription.Status == SubscriptionStatuses.Active && subscription.ExpiresAt > DateTime.UtcNow,
            PlanCode = subscription.PlanCode,
            PlanName = subscription.PlanName,
            StartedAt = subscription.StartedAt,
            ExpiresAt = subscription.ExpiresAt,
            LastPaymentAt = subscription.LastPaymentAt,
            AiVideoCreditsRemaining = subscription.AiVideoCreditsRemaining,
            ForumPostCreditsRemaining = subscription.ForumPostCreditsRemaining
        };
    }

    private static SubscriptionPaymentDto MapPayment(SubscriptionPayment payment)
    {
        return new SubscriptionPaymentDto
        {
            PaymentId = payment.PaymentId,
            PaymentCode = payment.PaymentCode,
            PlanCode = payment.PlanCode,
            PlanName = payment.PlanName,
            Amount = payment.Amount,
            Provider = payment.Provider,
            Status = payment.Status,
            BankCode = payment.BankCode,
            AccountNumber = payment.AccountNumber,
            AccountName = payment.AccountName,
            TransferContent = payment.TransferContent,
            QrUrl = payment.QrUrl,
            ExpiresAt = payment.ExpiresAt,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            SePayTransactionId = payment.SePayTransactionId,
            SePayReferenceCode = payment.SePayReferenceCode
        };
    }

    private static SubscriptionPaymentSseEventDto CreatePaymentEventDto(
        string eventName,
        string message,
        SubscriptionPayment payment,
        DateTime? occurredAt = null)
    {
        return new SubscriptionPaymentSseEventDto
        {
            Event = eventName,
            Message = message,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            Payment = MapPayment(payment)
        };
    }

    private static bool IsTerminalPaymentStatus(string paymentStatus)
    {
        return !string.Equals(paymentStatus, SubscriptionPaymentStatuses.Pending, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteKeepAliveAsync(CancellationToken cancellationToken)
    {
        await Response.WriteAsync(": keepalive\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteSseEventAsync(string eventName, SubscriptionPaymentSseEventDto payload, CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(payload);
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {jsonPayload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
