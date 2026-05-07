using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using VNFootballLeagues.Services.Services;

namespace VNFootballLeaguesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageProxyController> _logger;
    private readonly CloudinaryService _cloudinary;
    private readonly string _scraperApiKey;

    public ImageProxyController(IHttpClientFactory httpClientFactory, ILogger<ImageProxyController> logger, CloudinaryService cloudinary, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cloudinary = cloudinary;
        _scraperApiKey = configuration["SofascoreSettings:ScraperApiKey"] ?? string.Empty;
    }

    /// <summary>
    /// Proxy images from Sofascore API to bypass hotlink protection.
    /// Falls back to Cloudinary cache if Sofascore blocks the request.
    /// Usage: /api/ImageProxy/sofascore/team/12345
    ///        /api/ImageProxy/sofascore/player/67890
    ///        /api/ImageProxy/sofascore/tournament/626/dark
    /// </summary>
    [HttpGet("sofascore/team/{id}")]
    [ResponseCache(Duration = 0, NoStore = true)]
    public Task<IActionResult> GetTeamImage(string id) => ProxyImage("team", id, null);

    [HttpGet("sofascore/player/{id}")]
    [ResponseCache(Duration = 0, NoStore = true)]
    public Task<IActionResult> GetPlayerImage(string id) => ProxyImage("player", id, null);

    [HttpGet("sofascore/tournament/{id}/{theme?}")]
    [ResponseCache(Duration = 0, NoStore = true)]
    public Task<IActionResult> GetTournamentImage(string id, string? theme = "dark") => ProxyImage("tournament", id, theme);

    /// <summary>
    /// Nhận ảnh base64 từ browser (không bị Sofascore block) và cache lên Cloudinary.
    /// Body: { "type": "team|player|tournament", "id": "123", "theme": "dark", "dataUrl": "data:image/png;base64,..." }
    /// </summary>
    [HttpPost("sofascore/cache")]
    public async Task<IActionResult> CacheImage([FromBody] CacheImageRequest req)
    {
        if (string.IsNullOrEmpty(req.DataUrl) || string.IsNullOrEmpty(req.Type) || string.IsNullOrEmpty(req.Id))
            return BadRequest("Missing required fields");

        var cacheKey = req.Theme != null ? $"{req.Type}/{req.Id}/{req.Theme}" : $"{req.Type}/{req.Id}";

        try
        {
            // Parse base64 data URL
            var comma = req.DataUrl.IndexOf(',');
            if (comma < 0) return BadRequest("Invalid dataUrl format");

            var header = req.DataUrl[..comma]; // e.g. "data:image/png;base64"
            var base64 = req.DataUrl[(comma + 1)..];
            var contentType = header.Replace("data:", "").Replace(";base64", "");
            var imageBytes = Convert.FromBase64String(base64);

            var url = await _cloudinary.UploadSofascoreImageAsync(imageBytes, contentType, cacheKey);
            if (url == null) return StatusCode(500, "Cloudinary upload failed");

            _logger.LogInformation("Browser-cached Sofascore image: {Key} -> {Url}", cacheKey, url);
            return Ok(new { cached = true, url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching image: {Key}", cacheKey);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// Nhận type+id, tự fetch từ Sofascore và cache lên Cloudinary.
    /// Body: { "type": "team|player|tournament", "id": "123", "theme": "dark" }
    /// </summary>
    [HttpPost("sofascore/cache-by-id")]
    public async Task<IActionResult> CacheImageById([FromBody] CacheByIdRequest req)
    {
        if (string.IsNullOrEmpty(req.Type) || string.IsNullOrEmpty(req.Id))
            return BadRequest("Missing required fields");

        var cacheKey = req.Theme != null ? $"{req.Type}/{req.Id}/{req.Theme}" : $"{req.Type}/{req.Id}";

        // Nếu đã cache rồi thì check HEAD xem có thật sự tồn tại không
        var existing = _cloudinary.GetCachedUrl(cacheKey);
        if (existing != null)
        {
            try
            {
                var checkClient = _httpClientFactory.CreateClient();
                var head = await checkClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, existing));
                if (head.IsSuccessStatusCode)
                    return Ok(new { queued = false, cached = true, url = existing });
            }
            catch { /* nếu check fail thì vẫn tiếp tục upload */ }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var url = req.Type.ToLower() switch
                {
                    "team" => $"https://api.sofascore.app/api/v1/team/{req.Id}/image",
                    "player" => $"https://api.sofascore.app/api/v1/player/{req.Id}/image",
                    "tournament" => $"https://api.sofascore.app/api/v1/unique-tournament/{req.Id}/image/{req.Theme ?? "dark"}",
                    _ => null
                };
                if (url == null) return;

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Referer", "https://www.sofascore.com/");
                client.DefaultRequestHeaders.Add("Origin", "https://www.sofascore.com");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";
                await _cloudinary.UploadSofascoreImageAsync(imageBytes, contentType, cacheKey);
                _logger.LogInformation("Cached by-id: {Key}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed cache-by-id: {Key}", cacheKey);
            }
        });

        return Ok(new { queued = true });
    }

    public record CacheImageRequest(string Type, string Id, string? Theme, string DataUrl);
    public record CacheByIdRequest(string Type, string Id, string? Theme);


    private async Task<IActionResult> ProxyImage(string type, string id, string? theme)
    {
        var cacheKey = theme != null ? $"{type}/{id}/{theme}" : $"{type}/{id}";

        var sofascoreUrl = type.ToLower() switch
        {
            "team"       => $"https://api.sofascore.app/api/v1/team/{id}/image",
            "player"     => $"https://api.sofascore.app/api/v1/player/{id}/image",
            "tournament" => $"https://api.sofascore.app/api/v1/unique-tournament/{id}/image/{theme ?? "dark"}",
            _            => null
        };

        if (sofascoreUrl == null)
            return BadRequest("Invalid image type. Use: team, player, or tournament");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Referer", "https://www.sofascore.com/");
            client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");

            var response = await client.GetAsync(sofascoreUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Sofascore returned {StatusCode} for {Url}", (int)response.StatusCode, sofascoreUrl);
                return StatusCode((int)response.StatusCode);
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";

            // Upload Cloudinary fire-and-forget
            _ = Task.Run(async () =>
            {
                try
                {
                    await _cloudinary.UploadSofascoreImageAsync(imageBytes, contentType, cacheKey);
                    _logger.LogInformation("Cached to Cloudinary: {Key}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache to Cloudinary: {Key}", cacheKey);
                }
            });

            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying image: {Type}/{Id}", type, id);
            return StatusCode(500);
        }
    }
}
