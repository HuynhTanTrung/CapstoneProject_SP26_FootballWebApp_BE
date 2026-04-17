using Microsoft.AspNetCore.Mvc;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageProxyController> _logger;

    public ImageProxyController(IHttpClientFactory httpClientFactory, ILogger<ImageProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Proxy images from Sofascore API to bypass hotlink protection.
    /// Usage: /api/ImageProxy/sofascore/team/12345
    ///        /api/ImageProxy/sofascore/player/67890
    ///        /api/ImageProxy/sofascore/tournament/626/dark
    /// </summary>
    [HttpGet("sofascore/{type}/{id}/{theme?}")]
    [ResponseCache(Duration = 86400)] // Cache for 24 hours
    public async Task<IActionResult> GetSofascoreImage(string type, string id, string? theme = null)
    {
        try
        {
            // Build Sofascore URL
            var url = type.ToLower() switch
            {
                "team" => $"https://api.sofascore.app/api/v1/team/{id}/image",
                "player" => $"https://api.sofascore.app/api/v1/player/{id}/image",
                "tournament" => $"https://api.sofascore.app/api/v1/unique-tournament/{id}/image/{theme ?? "dark"}",
                _ => null
            };

            if (url == null)
                return BadRequest("Invalid image type. Use: team, player, or tournament");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";

            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying Sofascore image: {Type}/{Id}", type, id);
            return StatusCode(500);
        }
    }
}
