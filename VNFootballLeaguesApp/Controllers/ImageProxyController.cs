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
    [HttpGet("sofascore/team/{id}")]
    [ResponseCache(Duration = 86400)]
    public Task<IActionResult> GetTeamImage(string id) => ProxyImage("team", id, null);

    [HttpGet("sofascore/player/{id}")]
    [ResponseCache(Duration = 86400)]
    public Task<IActionResult> GetPlayerImage(string id) => ProxyImage("player", id, null);

    [HttpGet("sofascore/tournament/{id}/{theme?}")]
    [ResponseCache(Duration = 86400)]
    public Task<IActionResult> GetTournamentImage(string id, string? theme = "dark") => ProxyImage("tournament", id, theme);

    private async Task<IActionResult> ProxyImage(string type, string id, string? theme)
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
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Referer", "https://www.sofascore.com/");
            client.DefaultRequestHeaders.Add("Origin", "https://www.sofascore.com");
            client.DefaultRequestHeaders.Add("Accept", "image/webp,image/apng,image/*,*/*;q=0.8");
            
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Sofascore returned {StatusCode} for URL: {Url}", (int)response.StatusCode, url);
                return StatusCode((int)response.StatusCode, $"Sofascore returned {(int)response.StatusCode} for {url}");
            }

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
