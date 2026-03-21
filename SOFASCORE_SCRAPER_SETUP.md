# SofaScore Scraper Implementation

## Overview
This implementation uses PuppeteerSharp to bypass SSL/anti-bot protection when scraping SofaScore data.

## What Was Added

### 1. NuGet Package
- **PuppeteerSharp** (v24.39.0) - Added to `VNFootballLeagues.Services` project

### 2. Service Layer
- **Interface**: `VNFootballLeagues.Services/IServices/ISofascoreScraperService.cs`
- **Implementation**: `VNFootballLeagues.Services/Services/SofascoreScraperService.cs`

### 3. Controller
- **SofascoreController**: `VNFootballLeaguesApp/Controllers/SofascoreController.cs`

### 4. Dependency Injection
- Service registered in `Program.cs`

## How It Works

1. **Browser Download**: On first run, Chromium is automatically downloaded (~150MB)
2. **Headless Browser**: Launches a headless Chrome instance
3. **API Request**: Navigates to SofaScore API endpoint as a real browser
4. **JSON Extraction**: Extracts JSON response from the page
5. **Cleanup**: Properly closes browser and page resources

## API Endpoint

```
GET /api/sofascore/lineups?eventId=15485525
```

### Example Request
```bash
curl https://localhost:7000/api/sofascore/lineups?eventId=15485525
```

### Response
Returns raw JSON from SofaScore API containing lineup information.

## Testing

1. Build the solution:
```bash
dotnet build
```

2. Run the application:
```bash
dotnet run --project VNFootballLeaguesApp
```

3. Test the endpoint using Swagger UI or curl

## Important Notes

- **First Run**: Chromium download happens automatically (one-time, ~150MB)
- **Performance**: Browser automation is slower than direct HTTP requests
- **Resources**: Each request launches a browser instance - consider caching for production
- **Error Handling**: Includes comprehensive logging and exception handling
- **Thread Safety**: Browser download is thread-safe using SemaphoreSlim

## Production Considerations

1. **Caching**: Implement response caching to reduce scraping frequency
2. **Rate Limiting**: Add rate limiting to prevent abuse
3. **Browser Pooling**: Consider reusing browser instances for better performance
4. **Monitoring**: Add health checks and monitoring for browser processes
5. **Timeouts**: Adjust timeout values based on your needs (currently 30s)

## Troubleshooting

If you encounter issues:
- Check logs for detailed error messages
- Ensure sufficient disk space for Chromium download
- Verify network connectivity to SofaScore
- Check if SofaScore has changed their API structure
