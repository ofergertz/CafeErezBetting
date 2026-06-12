using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Scrapes live winner odds from livegames.co.il / telesport.co.il using a headless Chromium browser.
/// Both sites are JavaScript-rendered SPAs — a real browser is required to extract odds data.
/// Requires Playwright browser binaries: run `playwright install chromium` on the host or use the Docker image.
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
            Args     = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage",
                        "--disable-blink-features=AutomationControlled"],
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            Locale    = "he-IL",
        });

        var page = await context.NewPageAsync();

        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["Accept-Language"] = "he-IL,he;q=0.9,en;q=0.8",
            ["Referer"]         = "https://www.google.co.il/",
        });

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout   = 30_000,
            });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            logger.LogError("Network error reaching {Url}: {Error}", url, ex.Message);
            throw;
        }

        // Wait for JS to render match data (dynamic sites need extra time)
        await page.WaitForTimeoutAsync(4000);

        var matches = await ExtractMatchesAsync(page);

        logger.LogInformation("Playwright scraper finished — {Count} matches found", matches.Count);
        return matches;
    }

    // ── Extraction ─────────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> ExtractMatchesAsync(IPage page)
    {
        var results = new List<WinnerMatchDto>();
        var now     = DateTime.UtcNow;

        // Strategy 1: try known Israeli betting site selectors
        var candidates = new[]
        {
            ".winner-row", ".match-row", ".game-row", ".bet-row",
            "tr.match", "tr.game", "tr[class*='match']", "tr[class*='game']",
            "tr[class*='winner']", "[class*='matchRow']", "[class*='gameRow']",
        };

        IReadOnlyList<IElementHandle>? rows = null;
        foreach (var sel in candidates)
        {
            rows = await page.QuerySelectorAllAsync(sel);
            if (rows.Count > 0)
            {
                logger.LogInformation("Found {Count} rows with selector '{Sel}'", rows.Count, sel);
                break;
            }
        }

        // Strategy 2: fall back to all table rows
        if (rows == null || rows.Count == 0)
        {
            rows = await page.QuerySelectorAllAsync("tr");
            logger.LogWarning("No specific selectors matched — scanning all {Count} <tr> elements", rows.Count);
        }

        // Strategy 3: try div-based layout
        if (rows.Count == 0)
        {
            rows = await page.QuerySelectorAllAsync("div[class*='row'], div[class*='item'], div[class*='match']");
            logger.LogWarning("Trying div layout — {Count} elements found", rows.Count);
        }

        var index = 0;
        foreach (var row in rows)
        {
            try
            {
                var text = (await row.InnerTextAsync()).Trim();
                var match = TryParseMatchRow(text, now, index);
                if (match is not null)
                {
                    results.Add(match);
                    index++;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to parse row text");
            }

            if (index >= 50) break;
        }

        // If we still have nothing, log the page source for diagnostics
        if (results.Count == 0)
        {
            var title   = await page.TitleAsync();
            var bodyLen = (await page.InnerTextAsync("body")).Length;
            logger.LogWarning(
                "Scraper found 0 matches. Page title: '{Title}', body text length: {Len}. " +
                "Check WinnerScraper:Url and WinnerScraper:RowSelector in appsettings.",
                title, bodyLen);
        }

        return results;
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to parse a row's inner text into a WinnerMatchDto.
    /// Handles both tab-separated and whitespace-delimited formats.
    /// </summary>
    private WinnerMatchDto? TryParseMatchRow(string text, DateTime baseTime, int index)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Split on whitespace/tab/newline sequences
        var parts = text
            .Split(['\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p => p.Split("  ", StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        if (parts.Length < 5) return null;

        // Find 3 consecutive odds values (decimals between 1.01 and 49.99)
        var oddsValues  = new List<decimal>();
        var firstOddsIdx = -1;

        for (var i = 0; i < parts.Length; i++)
        {
            if (TryParseDecimal(parts[i], out var v) && v is >= 1.01m and <= 49.99m)
            {
                if (oddsValues.Count == 0) firstOddsIdx = i;
                oddsValues.Add(v);
                if (oddsValues.Count == 3) break;
            }
            else if (oddsValues.Count > 0 && oddsValues.Count < 3)
            {
                // Reset if sequence is broken
                oddsValues.Clear();
                firstOddsIdx = -1;
            }
        }

        if (oddsValues.Count < 3 || firstOddsIdx < 2) return null;

        // Everything before the first odds index = time + team names
        var metaParts = parts.Take(firstOddsIdx).ToArray();
        if (metaParts.Length < 2) return null;

        // Try to extract time from first token (format HH:mm)
        var scheduledAt = baseTime.AddHours(2);
        var startMeta   = 0;

        if (metaParts[0].Contains(':') && metaParts[0].Length <= 5 &&
            TimeSpan.TryParse(metaParts[0], out var ts))
        {
            var candidate = baseTime.Date.Add(ts);
            // If the time is in the past (today), assume tomorrow
            if (candidate < baseTime.AddMinutes(-30)) candidate = candidate.AddDays(1);
            scheduledAt = candidate;
            startMeta   = 1;
        }

        var teamParts = metaParts.Skip(startMeta).ToArray();
        if (teamParts.Length < 2) return null;

        // Split teams at midpoint — heuristic: home team is first half
        var mid      = Math.Max(1, teamParts.Length / 2);
        var homeTeam = string.Join(" ", teamParts.Take(mid)).Trim();
        var awayTeam = string.Join(" ", teamParts.Skip(mid)).Trim();

        if (homeTeam.Length < 2 || awayTeam.Length < 2) return null;

        // Determine league from surrounding context (not always available in row text)
        var league  = ExtractLeague(parts, firstOddsIdx) ?? "ווינר";
        var isLive  = scheduledAt <= DateTime.UtcNow;
        var status  = isLive ? "live" : "upcoming";

        logger.LogDebug("Parsed match #{Index}: {Home} vs {Away} @ {At:HH:mm} odds={O1}/{OX}/{O2}",
            index, homeTeam, awayTeam, scheduledAt, oddsValues[0], oddsValues[1], oddsValues[2]);

        return new WinnerMatchDto(
            Guid.NewGuid(),
            $"scraped-{index:000}",
            homeTeam,
            awayTeam,
            league,
            scheduledAt,
            new OddsDto(oddsValues[0], oddsValues[1], oddsValues[2]),
            status,
            isLive,
            DateTime.UtcNow
        );
    }

    private static string? ExtractLeague(string[] parts, int beforeIdx)
    {
        // League often appears right after the odds block or as a separate token
        // Look for known Israeli league names
        var text = string.Join(" ", parts.Take(beforeIdx));
        if (text.Contains("ליגת העל", StringComparison.OrdinalIgnoreCase)) return "ליגת העל";
        if (text.Contains("ליגה א",  StringComparison.OrdinalIgnoreCase)) return "ליגה א'";
        if (text.Contains("Premier",  StringComparison.OrdinalIgnoreCase)) return "Premier League";
        if (text.Contains("La Liga",  StringComparison.OrdinalIgnoreCase)) return "La Liga";
        if (text.Contains("Champions",StringComparison.OrdinalIgnoreCase)) return "Champions League";
        return null;
    }

    private static bool TryParseDecimal(string s, out decimal result)
    {
        // Handle both "2.10" (dot) and "2,10" (comma as decimal separator)
        var normalized = s.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
    }
}
