using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNFootballLeagues.Services.Dtos;
using VNFootballLeagues.Services.IServices;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/ai-analysis")]
[Authorize]
public class AIAnalysisController : ControllerBase
{
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly ILogger<AIAnalysisController> _logger;

    public AIAnalysisController(
        IAIAnalysisService aiAnalysisService,
        ILogger<AIAnalysisController> logger)
    {
        _aiAnalysisService = aiAnalysisService;
        _logger = logger;
    }

    [HttpPost("player-rating")]
    public async Task<IActionResult> AnalyzePlayerRating([FromBody] PlayerRatingAnalysisRequest request)
    {
        try
        {
            var result = await _aiAnalysisService.AnalyzePlayerRatingAsync(
                request.MatchId,
                request.PlayerId,
                HttpContext.RequestAborted);

            if (!result.Success)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    success = false,
                    message = result.AnalysisVi
                });
            }

            return Ok(result);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI player rating analysis failed for MatchId={MatchId}, PlayerId={PlayerId}", request.MatchId, request.PlayerId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    [HttpPost("match")]
    public async Task<IActionResult> AnalyzeMatch([FromBody] MatchAnalysisRequest request)
    {
        try
        {
            var result = await _aiAnalysisService.AnalyzeMatchAsync(
                request.MatchId,
                HttpContext.RequestAborted);

            if (!result.Success)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    success = false,
                    message = result.AnalysisVi
                });
            }

            return Ok(result);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI match analysis failed for MatchId={MatchId}", request.MatchId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}
