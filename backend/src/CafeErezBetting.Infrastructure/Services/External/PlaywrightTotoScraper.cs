using System.Text.RegularExpressions;
using Microsoft.Playwright;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Scrapes Toto round data from telesport.co.il or livegames.co.il using Playwright.
/// Tries URLs in order; on 0 results falls back to the next URL automatically.
/// Configurable via appsettings Scrapers:Toto section.
/// </summary>
public class PlaywrightTotoScraper(
    IConfiguration config,
    ILogger<PlaywrightTotoScraper> logger
)
{
    private static readonly string[] DefaultUrls =
    [
        "https://www.telesport.co.il/%D7%98%D7%95%D7%98%D7%95",
        "https://www.livegames.co.il/toto.aspx",
    ];

    private static readonly HashSet<string> SkipWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "הסתיים", "לא התחיל", "בשידור חי", "נדחה", "בוטל", "מושהה", "הפסקה",
        "True", "False",
        "1X2", "1", "X", "2",
    };

    private string[] GetConfigUrls()
    {
        var urls = config.GetSection("Scrapers:Toto:Urls").Get<string[]>();
        return urls?.Length > 0 ? urls : DefaultUrls;
    }

    public async Task<TotoRoundDto?> ScrapeAsync(CancellationToken ct = default)
    {
        var urls         = GetConfigUrls();
        var chromiumPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");

        logger.LogInformation("Playwright Toto scraper starting — {Count} URL(s) configured", urls.Length);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless       = true,
            ExecutablePath = chromiumPath,
            Args           =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-blink-features=AutomationControlled",
                "--disable-extensions",
                "--window-size=1280,800",
            ],
        });

        foreach (var url in urls)
        {
            try
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                    Locale            = "he-IL",
                    ViewportSize      = new ViewportSize { Width = 1280, Height = 800 },
                    JavaScriptEnabled = true,
                });

                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                    window.chrome = { runtime: {} };
                ");

                var page = await context.NewPageAsync();
                await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
                {
                    ["Accept-Language"] = "he-IL,he;q=0.9,en;q=0.8",
                    ["Referer"]         = "https://www.google.co.il/",
                });

                var round = await TryScrapePageAsync(page, url);
                await context.CloseAsync();

                if (round is not null && round.Matches.Count > 0)
                {
                    logger.LogInformation("Playwright Toto scraper finished — {Count} matches from {Url}", round.Matches.Count, url);
                    return round;
                }

                logger.LogWarning("0 Toto matches from {Url} — trying next URL", url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Toto scrape failed for {Url} — trying next URL", url);
            }
        }

        logger.LogWarning("All Toto URLs returned 0 matches");
        return null;
    }

    // ── Per-URL navigation + extraction ──────────────────────────────────────

    private async Task<TotoRoundDto?> TryScrapePageAsync(IPage page, string url)
    {
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout   = 45_000,
            });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            logger.LogError("Network error reaching {Url}: {Error}", url, ex.Message);
            throw;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Timeout"))
        {
            logger.LogWarning("Navigation timeout on {Url} — attempting to parse anyway", url);
        }

        await page.WaitForTimeoutAsync(5000);

        logger.LogInformation("Toto page loaded: title='{Title}', url='{Url}'",
            await page.TitleAsync(), page.Url);

        var roundNumber = await ExtractRoundNumberAsync(page);
        var matches     = await ExtractMatchesAsync(page, url, roundNumber);

        if (matches.Count == 0)
            await TakeDebugScreenshotAsync(page);

        if (matches.Count == 0)
            return null;

        var roundId = $"toto-{roundNumber}";
        return new TotoRoundDto(roundId, roundNumber, matches);
    }

    // ── Round number extraction ───────────────────────────────────────────────

    private async Task<int> ExtractRoundNumberAsync(IPage page)
    {
        var headings = await page.QuerySelectorAllAsync("h1, h2, h3");
        foreach (var heading in headings)
        {
            try
            {
                var text = (await heading.InnerTextAsync()).Trim();
                var m    = Regex.Match(text, @"(?:טוטו\s*)?(\d{3,5})");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var num))
                {
                    logger.LogInformation("Toto round number detected: {Num} from '{Text}'", num, text);
                    return num;
                }
            }
            catch { /* ignore */ }
        }

        // Try page title
        var title = await page.TitleAsync();
        var titleMatch = Regex.Match(title, @"(\d{3,5})");
        if (titleMatch.Success && int.TryParse(titleMatch.Groups[1].Value, out var titleNum))
        {
            logger.LogInformation("Toto round number from title: {Num}", titleNum);
            return titleNum;
        }

        logger.LogWarning("Could not detect Toto round number — defaulting to 0");
        return 0;
    }

    // ── Match extraction ──────────────────────────────────────────────────────

    private async Task<List<TotoMatchDto>> ExtractMatchesAsync(IPage page, string url, int roundNumber)
    {
        var results = new List<TotoMatchDto>();

        // Try known selectors first
        var specificSelectors = new[]
        {
            ".toto-row", ".match-row", ".game-row",
            "tr[class*='toto']", "tr[class*='match']", "tr[class*='game']",
            "[class*='matchRow']", "[class*='gameRow']",
        };

        IReadOnlyList<IElementHandle>? specificRows = null;
        foreach (var sel in specificSelectors)
        {
            specificRows = await page.QuerySelectorAllAsync(sel);
            if (specificRows.Count > 0)
            {
                logger.LogInformation("Toto: specific selector '{Sel}' matched {Count} rows", sel, specificRows.Count);
                break;
            }
        }

        var rows = specificRows?.Count > 0
            ? specificRows
            : await page.QuerySelectorAllAsync("tr");

        if (rows.Count == 0)
        {
            rows = await page.QuerySelectorAllAsync(
                "div[class*='row'], div[class*='match'], div[class*='game'], div[class*='item']");
            logger.LogWarning("Toto: table rows empty — div fallback found {Count} elements", rows.Count);
        }

        if (rows.Count == 0)
        {
            logger.LogWarning("Toto: no rows found on {Url}", url);
            return results;
        }

        logger.LogInformation("Toto: processing {Count} candidate rows", rows.Count);

        var index = 0;
        foreach (var row in rows)
        {
            try
            {
                var cells = await row.QuerySelectorAllAsync("td");
                TotoMatchDto? match = null;

                if (cells.Count >= 2)
                {
                    var cellTexts = new List<string>();
                    foreach (var cell in cells)
                        cellTexts.Add((await cell.InnerTextAsync()).Trim());

                    match = TryParseFromCells(cellTexts, roundNumber, index);
                }

                if (match is null)
                {
                    var rowText = (await row.InnerTextAsync()).Trim();
                    match = TryParseFromText(rowText, roundNumber, index);
                }

                if (match is not null)
                {
                    results.Add(match);
                    index++;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Toto row parse error");
            }

            if (index >= 13) break;
        }

        return results;
    }

    // ── Cell-based parser ─────────────────────────────────────────────────────

    private TotoMatchDto? TryParseFromCells(List<string> cells, int roundNumber, int index)
    {
        var textCells = new List<string>();

        foreach (var raw in cells)
        {
            var cell = raw.Trim();
            if (string.IsNullOrWhiteSpace(cell)) continue;

            // Pure integer → match/round number, skip
            if (int.TryParse(cell, out _)) continue;

            // ".223" → dot-prefixed form number, skip
            if (Regex.IsMatch(cell, @"^\.\d+")) continue;

            // Known skip words
            if (SkipWords.Contains(cell)) continue;

            // Parenthetical-only like "(6)"
            if (Regex.IsMatch(cell, @"^\([^)]+\)$")) continue;

            // All-ASCII short codes
            if (Regex.IsMatch(cell, @"^[A-Z0-9,./]+$") && cell.Length <= 5) continue;

            // Time token HH:mm
            if (IsTimeToken(cell)) continue;

            // Score token like "2:3"
            if (IsScoreToken(cell)) continue;

            // Decimal → odds column, skip (Toto doesn't need odds)
            if (TryParseDecimal(cell, out _)) continue;

            // Must contain at least one letter
            if (!HasLetterChar(cell)) continue;

            // "HomeTeam - AwayTeam" in one cell → split
            if (cell.Contains(" - "))
            {
                var sep   = cell.IndexOf(" - ", StringComparison.Ordinal);
                var part1 = cell[..sep].Trim();
                var part2 = cell[(sep + 3)..].Trim();
                if (part1.Length >= 2) textCells.Add(part1);
                if (part2.Length >= 2) textCells.Add(part2);
                continue;
            }

            if (cell.Length >= 2)
                textCells.Add(cell);
        }

        if (textCells.Count < 2) return null;

        var homeTeam = textCells[0].Trim();
        var awayTeam = textCells[^1].Trim();
        if (homeTeam.Length < 2 || awayTeam.Length < 2 || homeTeam == awayTeam) return null;

        var matchId = $"toto-{roundNumber}-{index:00}";

        logger.LogDebug("[Toto Cell] #{Index} {Home} v {Away}", index, homeTeam, awayTeam);

        return new TotoMatchDto(matchId, homeTeam, awayTeam, "טוטו");
    }

    // ── Text-based parser (fallback) ──────────────────────────────────────────

    private TotoMatchDto? TryParseFromText(string text, int roundNumber, int index)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text
            .Split(['\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p => Regex.Split(p, @"\s{2,}"))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        var textParts = parts
            .Where(p => !int.TryParse(p, out _)
                     && !Regex.IsMatch(p, @"^\.\d+")
                     && !SkipWords.Contains(p)
                     && !IsTimeToken(p)
                     && !IsScoreToken(p)
                     && !TryParseDecimal(p, out _)
                     && HasLetterChar(p)
                     && p.Length >= 2)
            .ToList();

        if (textParts.Count < 2) return null;

        // If a part contains " - " split it
        var expanded = new List<string>();
        foreach (var part in textParts)
        {
            if (part.Contains(" - "))
            {
                var sep = part.IndexOf(" - ", StringComparison.Ordinal);
                expanded.Add(part[..sep].Trim());
                expanded.Add(part[(sep + 3)..].Trim());
            }
            else
            {
                expanded.Add(part);
            }
        }

        if (expanded.Count < 2) return null;

        var homeTeam = expanded[0];
        var awayTeam = expanded[^1];
        if (homeTeam.Length < 2 || awayTeam.Length < 2 || homeTeam == awayTeam) return null;

        var matchId = $"toto-{roundNumber}-{index:00}";

        logger.LogDebug("[Toto Text] #{Index} {Home} v {Away}", index, homeTeam, awayTeam);

        return new TotoMatchDto(matchId, homeTeam, awayTeam, "טוטו");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsTimeToken(string s)
        => s.Length is >= 4 and <= 5
           && s.Contains(':')
           && TimeSpan.TryParse(s, out _);

    private static bool IsScoreToken(string s)
    {
        var idx = s.IndexOf(':');
        if (idx < 1 || idx >= s.Length - 1) return false;
        return int.TryParse(s.AsSpan(0, idx), out _)
            && int.TryParse(s.AsSpan(idx + 1), out _);
    }

    private static bool HasLetterChar(string s)
    {
        foreach (var c in s)
            if (char.IsLetter(c)) return true;
        return false;
    }

    private static bool TryParseDecimal(string s, out decimal result)
    {
        var normalized = s.Replace(',', '.');
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }

    private async Task TakeDebugScreenshotAsync(IPage page)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(),
                $"toto-scraper-debug-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            logger.LogWarning("Toto debug screenshot saved → {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not save Toto debug screenshot");
        }
    }
}
