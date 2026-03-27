using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.IServices;
using VNFootballLeagues.Services.Models.Subscriptions;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/sepay/webhook")]
public class SePayWebhookController : ControllerBase
{
    private readonly ISePayWebhookService _sePayWebhookService;

    public SePayWebhookController(ISePayWebhookService sePayWebhookService)
    {
        _sePayWebhookService = sePayWebhookService;
    }

    [HttpPost("payments")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceivePaymentWebhook([FromBody] SePayWebhookPayload payload)
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();
        var result = await _sePayWebhookService.ProcessAsync(payload, authorizationHeader);

        return StatusCode(result.StatusCode, new
        {
            success = result.Success
        });
    }
}
