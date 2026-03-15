using PuppeteerSharp;
using VNFootballLeagues.Services.IServices;
using Microsoft.Extensions.Logging;

namespace VNFootballLeagues.Services.Services;

/// <summary>
/// Service for scraping SofaScore data using PuppeteerSharp browser automation
/// Bypasses SSL/anti-bot protection by using a real browser instance
/// </summary>
public class SofascoreScraperService : ISofascoreScraperService
{
    private readonly ILogger<SofascoreScraperService> _logger;
    private static bool _browserDownloaded = false;
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    public SofascoreScraperService(ILogger<SofascoreScraperService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fetches match lineup data from SofaScore API using browser automation
    /// </summary>
    /// <param name="eventId">The SofaScore event/match ID</param>
    /// <returns>JSON string containing lineup data</returns>
    /// <exception cref="Exception">Thrown when scraping fails</exception>
    public async Task<string> GetMatchLineupsAsync(int eventId)
    {
        IBrowser? browser = null;
        IPage? page = null;

        try
        {
            // Ensure Chromium is downloaded (only happens once)
            await EnsureBrowserDownloadedAsync();

            // Launch headless Chromium browser with additional stealth options
            _logger.LogInformation("Launching headless browser for event {EventId}", eventId);
            browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-accelerated-2d-canvas",
                    "--disable-gpu",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-features=IsolateOrigins,site-per-process"
                }
            });

            // Create a new page
            page = await browser.NewPageAsync();

            // Set realistic viewport
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1920,
                Height = 1080
            });

            // Set user agent and headers to mimic a real browser
            await page.SetUserAgentAsync(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );

            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                { "Accept", "application/json, text/plain, */*" },
                { "Accept-Language", "en-US,en;q=0.9" },
                { "Accept-Encoding", "gzip, deflate, br" }
            });

            // STRATEGY: First visit main site to establish session and cookies
            _logger.LogInformation("Establishing session by visiting main site");
            await page.GoToAsync("https://www.sofascore.com/", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 30000
            });

            // Wait for page to settle
            await Task.Delay(2000);

            // Now navigate to the API endpoint with established session
            string url = $"https://www.sofascore.com/api/v1/event/{eventId}/lineups";
            _logger.LogInformation("Navigating to API endpoint: {Url}", url);

            // Use page.EvaluateFunctionAsync to fetch via XHR/fetch instead of navigation
            string jsonResponse = await page.EvaluateFunctionAsync<string>($@"
                async () => {{
                    try {{
                        const response = await fetch('{url}', {{
                            method: 'GET',
                            headers: {{
                                'Accept': 'application/json',
                                'Accept-Language': 'en-US,en;q=0.9'
                            }},
                            credentials: 'include'
                        }});
                        
                        if (!response.ok) {{
                            throw new Error(`HTTP ${{response.status}}: ${{response.statusText}}`);
                        }}
                        
                        const data = await response.text();
                        return data;
                    }} catch (error) {{
                        throw new Error(`Fetch failed: ${{error.message}}`);
                    }}
                }}
            ");

            // Validate we got JSON
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                throw new Exception("No content retrieved from the API");
            }

            // Basic JSON validation
            if (!jsonResponse.TrimStart().StartsWith("{") && !jsonResponse.TrimStart().StartsWith("["))
            {
                _logger.LogWarning("Response doesn't appear to be JSON: {Response}", 
                    jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length)));
                throw new Exception("Response is not valid JSON");
            }

            _logger.LogInformation("Successfully retrieved lineup data for event {EventId} ({Length} characters)", 
                eventId, jsonResponse.Length);

            return jsonResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping lineup data for event {EventId}", eventId);
            throw new Exception($"Failed to scrape lineup data for event {eventId}: {ex.Message}", ex);
        }
        finally
        {
            // Clean up resources
            if (page != null)
            {
                try { await page.CloseAsync(); } catch { /* Ignore cleanup errors */ }
            }

            if (browser != null)
            {
                try { await browser.CloseAsync(); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Ensures Chromium browser is downloaded before first use
    /// Thread-safe implementation to prevent multiple simultaneous downloads
    /// </summary>
    private async Task EnsureBrowserDownloadedAsync()
    {
        if (_browserDownloaded)
        {
            return;
        }

        await _downloadLock.WaitAsync();
        try
        {
            if (_browserDownloaded)
            {
                return;
            }

            _logger.LogInformation("Downloading Chromium browser (first run only)...");

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            _browserDownloaded = true;
            _logger.LogInformation("Chromium browser downloaded successfully");
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
