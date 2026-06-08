using Microsoft.Playwright;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Scrapes live winner odds from telesport.co.il using a headless Chromium browser.
/// Both livegames.co.il and telesport.co.il are JavaScript-rendered SPAs —
/// a real browser is required to extract odds data.
/// </summary>
public class PlaywrightWinnerScraper(
    IConfiguration config,
    ILogger<PlaywrightWinnerScraper> logger
)
{
    private static readonly string DefaultUrl =
        "https://www.telesport.co.il/winnerzonepage.aspx";

    public async Task<List<WinnerMatchDto>> ScrapeAsync(CancellationToken ct = default)
    {
        var url = config["WinnerScraper:Url"] ?? DefaultUrl;

        logger.LogInformation("Playwright scraper starting → {Url}", url);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args     = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"],
        });

        var page = await browser.NewPageAsync();

        // Mimic a real browser to avoid bot detection
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["Accept-Language"] = "he-IL,he;q=0.9,en;q=0.8",
            ["User-Agent"]      = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                                  "AppleWebKit/537.36 (KHTML, like Gecko) " +
                                  "Chrome/120.0.0.0 Safari/537.36",
        });

        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout   = 30_000,
        });

        // Give JS time to render match data
        await page.WaitForTimeoutAsync(3000);

        var matches = await ExtractMatchesAsync(page);

        logger.LogInformation("Playwright scraper finished — {Count} matches found", matches.Count);
        return matches;
    }

    // ── Extraction ─────────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> ExtractMatchesAsync(IPage page)
    {
        var results = new List<WinnerMatchDto>();

        // Try to extract from the rendered page using broad selectors.
        // telesport winner zone shows matches in table rows with odds columns.
        // Selectors can be refined via appsettings WinnerScraper:RowSelector etc.
        var rowSelector  = config["WinnerScraper:RowSelector"]  ?? "tr[class*='winner'], tr[class*='match'], .match-row, .game-row";
        var rows         = await page.QuerySelectorAllAsync(rowSelector);

        if (rows.Count == 0)
        {
            // Fallback: try any table row that contains odds-like numbers
            rows = await page.QuerySelectorAllAsync("tr");
            logger.LogWarning("Primary selector found 0 rows, falling back to all <tr> — found {Count}", rows.Count);
        }

        var now = DateTime.UtcNow;
        var index = 0;

        foreach (var row in rows)
        {
            try
            {
                var text = await row.InnerTextAsync();
                var match = TryParseMatchRow(text, now, index);
                if (match is not null)
                {
                    results.Add(match);
                    index++;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to parse row");
            }

            if (index >= 50) break; // safety cap
        }

        return results;
    }

    /// <summary>
    /// Attempts to parse a table row text into a WinnerMatchDto.
    /// Expected format (telesport): "TIME HOME AWAY ODDS1 ODDSX ODDS2"
    /// e.g. "21:45 מכבי תל אביב הפועל ב\"ש 2.10 3.20 3.50"
    /// </summary>
    private static WinnerMatchDto? TryParseMatchRow(string text, DateTime baseTime, int index)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split(['\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return null;

        // Look for 3 consecutive decimal numbers — those are the odds
        var oddsValues = new List<decimal>();
        var oddsIndexes = new List<int>();

        for (var i = 0; i < parts.Length; i++)
        {
            if (decimal.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)
                && v is > 1.0m and < 50.0m)
            {
                oddsValues.Add(v);
                oddsIndexes.Add(i);
                if (oddsValues.Count == 3) break;
            }
        }

        if (oddsValues.Count < 3) return null;

        // Everything before first odds index = metadata (time, teams)
        var metaParts = parts.Take(oddsIndexes[0]).ToArray();
        if (metaParts.Length < 2) return null;

        // Try to extract time from first token
        var scheduledAt = baseTime.AddHours(2); // default
        if (metaParts[0].Contains(':') && metaParts[0].Length <= 5)
        {
            if (TimeSpan.TryParse(metaParts[0], out var ts))
                scheduledAt = baseTime.Date.Add(ts);
            metaParts = metaParts.Skip(1).ToArray();
        }

        if (metaParts.Length < 2) return null;

        var midPoint = metaParts.Length / 2;
        var homeTeam = string.Join(" ", metaParts.Take(midPoint)).Trim();
        var awayTeam = string.Join(" ", metaParts.Skip(midPoint)).Trim();

        if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            return null;

        var isLive = scheduledAt <= DateTime.UtcNow;

        return new WinnerMatchDto(
            Guid.NewGuid(),
            $"ts-{index:000}",
            homeTeam,
            awayTeam,
            "ווינר",
            scheduledAt,
            new OddsDto(oddsValues[0], oddsValues[1], oddsValues[2]),
            isLive ? "live" : "upcoming",
            isLive,
            DateTime.UtcNow
        );
    }
}
