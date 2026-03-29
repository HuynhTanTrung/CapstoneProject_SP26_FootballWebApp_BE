using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Repositories.Models;
using VNFootballLeagues.Services.IServices;
using VNFootballLeaguesApp.DTOs.Common;
using VNFootballLeaguesApp.DTOs.Subscription;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Policy = "AdminOnly")]
public class AdminSubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IUserService _userService;

    public AdminSubscriptionController(ISubscriptionService subscriptionService, IUserService userService)
    {
        _subscriptionService = subscriptionService;
        _userService = userService;
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] AdminSubscriptionPaymentQueryDto queryDto)
    {
        var result = await _subscriptionService.GetPaymentsForAdminAsync(
            queryDto.Status,
            queryDto.PaymentCode,
            queryDto.Keyword,
            queryDto.PageNumber,
            queryDto.PageSize);

        return Ok(new ApiResponseDto<PagedResultDto<AdminSubscriptionPaymentDto>>
        {
            Success = true,
            Message = "Subscription payments fetched successfully.",
            Data = new PagedResultDto<AdminSubscriptionPaymentDto>
            {
                Items = result.Payments.Select(MapAdminPayment).ToList(),
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages
            }
        });
    }

    [HttpGet("payments/{paymentCode}")]
    public async Task<IActionResult> GetPayment(string paymentCode)
    {
        var payment = await _subscriptionService.GetPaymentByCodeForAdminAsync(paymentCode);
        if (payment is null)
        {
            return NotFound(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Subscription payment was not found."
            });
        }

        return Ok(new ApiResponseDto<AdminSubscriptionPaymentDto>
        {
            Success = true,
            Message = "Subscription payment fetched successfully.",
            Data = MapAdminPayment(payment)
        });
    }

    [HttpPatch("payments/{paymentCode}/manual-status")]
    public async Task<IActionResult> UpdatePaymentStatus(
        string paymentCode,
        [FromBody] AdminManualUpdateSubscriptionPaymentRequestDto requestDto)
    {
        var adminUserId = _userService.GetUserId(User);
        if (adminUserId is null)
        {
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Current admin user could not be resolved."
            });
        }

        var result = await _subscriptionService.ManuallyUpdatePaymentStatusAsync(
            adminUserId.Value,
            paymentCode,
            requestDto.Status,
            requestDto.Reason,
            requestDto.PaidAt,
            requestDto.ReferenceCode,
            requestDto.Gateway);

        if (!result.Success || result.Payment is null)
        {
            return StatusCode(result.StatusCode, new ApiResponseDto<object>
            {
                Success = false,
                Message = result.Message,
                Errors = result.Errors
            });
        }

        return Ok(new ApiResponseDto<AdminSubscriptionPaymentDto>
        {
            Success = true,
            Message = result.Message,
            Data = MapAdminPayment(result.Payment)
        });
    }

    private static AdminSubscriptionPaymentDto MapAdminPayment(SubscriptionPayment payment)
    {
        return new AdminSubscriptionPaymentDto
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
            SePayReferenceCode = payment.SePayReferenceCode,
            UserId = payment.UserId,
            Username = payment.User?.Username ?? string.Empty,
            Email = payment.User?.Email ?? string.Empty,
            FullName = payment.User?.FullName ?? string.Empty,
            ManualUpdatedByUserId = payment.ManualUpdatedByUserId,
            ManualUpdatedByName = payment.ManualUpdatedByName,
            ManualUpdatedAt = payment.ManualUpdatedAt,
            ManualUpdateReason = payment.ManualUpdateReason
        };
    }
}
