using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Scrapes live winner odds from telesport.co.il or livegames.co.il using Playwright.
/// Configurable via appsettings WinnerScraper section.
/// Both sites require a real browser (JavaScript rendering + bot protection).
///
/// Prerequisites: run `playwright install chromium` or use the provided Docker image
/// (PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium).
/// </summary>
public class PlaywrightWinnerScraper(
    IConfiguration config,
    ILogger<PlaywrightWinnerScraper> logger
)
{
    private static readonly string DefaultUrl =
        "https://www.telesport.co.il/winnerzonepage.aspx";

    // Known Israeli league names for league detection
    private static readonly (string Keyword, string Display)[] LeagueKeywords =
    [
        ("ליגת העל",   "ליגת העל"),
        ("ליגה א",     "ליגה א'"),
        ("ליגה ב",     "ליגה ב'"),
        ("Premier",    "Premier League"),
        ("La Liga",    "La Liga"),
        ("Champions",  "Champions League"),
        ("Europa",     "Europa League"),
        ("Serie A",    "Serie A"),
        ("Bundesliga", "Bundesliga"),
        ("Ligue 1",    "Ligue 1"),
    ];

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<List<WinnerMatchDto>> ScrapeAsync(CancellationToken ct = default)
    {
        var url = config["WinnerScraper:Url"] ?? DefaultUrl;
        logger.LogInformation("Playwright scraper starting → {Url}", url);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args     = [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-blink-features=AutomationControlled",
            ],
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

        // Give JS extra time to render dynamic content
        await page.WaitForTimeoutAsync(4000);

        var matches = await ExtractMatchesAsync(page, url);
        logger.LogInformation("Playwright scraper finished — {Count} matches found", matches.Count);
        return matches;
    }

    // ── Row extraction ────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> ExtractMatchesAsync(IPage page, string url)
    {
        var results     = new List<WinnerMatchDto>();
        var now         = DateTime.UtcNow;
        var rowSelector = config["WinnerScraper:RowSelector"] ?? "tr";

        // ── Step 1: choose row elements ───────────────────────────────────────
        IReadOnlyList<IElementHandle> rows;

        // First try any site-specific selectors from config / common patterns
        var specificSelectors = new[]
        {
            ".winner-row", ".match-row", ".game-row", ".bet-row",
            "tr[class*='winner']", "tr[class*='match']", "tr[class*='game']",
            "[class*='matchRow']", "[class*='gameRow']",
        };

        IReadOnlyList<IElementHandle>? specificRows = null;
        foreach (var sel in specificSelectors)
        {
            specificRows = await page.QuerySelectorAllAsync(sel);
            if (specificRows.Count > 0)
            {
                logger.LogInformation("Specific selector '{Sel}' matched {Count} rows", sel, specificRows.Count);
                break;
            }
        }

        rows = specificRows?.Count > 0
            ? specificRows
            : await page.QuerySelectorAllAsync(rowSelector);

        // Div-based layout fallback (some SPAs don't use tables)
        if (rows.Count == 0)
        {
            rows = await page.QuerySelectorAllAsync(
                "div[class*='row'], div[class*='match'], div[class*='game'], div[class*='item']");
            logger.LogWarning("Table rows empty — div fallback found {Count} elements", rows.Count);
        }

        if (rows.Count == 0)
        {
            await LogPageDiagnosticsAsync(page, url);
            return results;
        }

        logger.LogInformation("Processing {Count} candidate rows", rows.Count);

        // ── Step 2: log first 3 rows for diagnostics ──────────────────────────
        await LogSampleRowsAsync(rows, maxRows: 3);

        // ── Step 3: parse each row ────────────────────────────────────────────
        var index = 0;
        foreach (var row in rows)
        {
            try
            {
                // Primary: cell-based parsing (reliable for table layouts)
                var cells = await row.QuerySelectorAllAsync("td");
                WinnerMatchDto? match = null;

                if (cells.Count >= 3)
                {
                    var cellTexts = new List<string>();
                    foreach (var cell in cells)
                        cellTexts.Add((await cell.InnerTextAsync()).Trim());

                    match = TryParseFromCells(cellTexts, now, index);
                }

                // Fallback: text-based parsing (works for div/span layouts)
                if (match is null)
                {
                    var rowText = (await row.InnerTextAsync()).Trim();
                    match = TryParseFromText(rowText, now, index);
                }

                if (match is not null)
                {
                    results.Add(match);
                    index++;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Row parse error");
            }

            if (index >= 50) break;
        }

        if (results.Count == 0)
            await LogPageDiagnosticsAsync(page, url);

        return results;
    }

    // ── Cell-based parser (primary — works for any <table> layout) ────────────

    /// <summary>
    /// Parses a list of &lt;td&gt; cell values from a single row.
    /// Strategy: identify cells by content type — odds (1.01–49.99), time (HH:mm),
    /// pure integers (match numbers to skip), and text (team / league).
    /// This is site-agnostic and handles any column ordering.
    /// </summary>
    private WinnerMatchDto? TryParseFromCells(List<string> cells, DateTime baseTime, int index)
    {
        var odds      = new List<decimal>();
        var textCells = new List<string>();
        var timeCell  = (string?)null;

        foreach (var raw in cells)
        {
            var cell = raw.Trim();
            if (string.IsNullOrWhiteSpace(cell)) continue;

            // Pure integer → match/round number, skip
            if (int.TryParse(cell, out _)) continue;

            // HH:mm or H:mm → time
            if (IsTimeToken(cell) && timeCell is null)
            {
                timeCell = cell;
                continue;
            }

            // Decimal in odds range → collect as odds
            if (TryParseDecimal(cell, out var d) && d is >= 1.01m and <= 49.99m)
            {
                odds.Add(d);
                continue;
            }

            // Anything else that's long enough → team name or league
            if (cell.Length >= 2)
                textCells.Add(cell);
        }

        // Need exactly 3 odds and at least 2 text cells (home + away teams)
        if (odds.Count < 3 || textCells.Count < 2) return null;

        var scheduledAt = ParseTime(timeCell, baseTime);
        var league      = DetectLeague(textCells) ?? "ווינר";

        // Remove league from text cells — remaining are team names
        var teamCells = textCells.Where(t => t != league).ToList();
        if (teamCells.Count < 2) teamCells = textCells; // fallback

        var homeTeam = teamCells[0].Trim();
        var awayTeam = teamCells[^1].Trim();   // last text cell

        if (homeTeam.Length < 2 || awayTeam.Length < 2 || homeTeam == awayTeam) return null;

        var isLive = scheduledAt <= DateTime.UtcNow;

        logger.LogDebug("[Cell] #{Index} {Home} v {Away} {O1}/{OX}/{O2} @ {At:HH:mm}",
            index, homeTeam, awayTeam, odds[0], odds[1], odds[2], scheduledAt);

        return Build(index, homeTeam, awayTeam, league, scheduledAt, odds[0], odds[1], odds[2], isLive);
    }

    // ── Text-based parser (fallback — for div/span layouts) ───────────────────

    /// <summary>
    /// Parses a row's raw inner text when no &lt;td&gt; cells exist.
    /// Splits on tab/newline, then looks for 3 consecutive odds values.
    /// </summary>
    private WinnerMatchDto? TryParseFromText(string text, DateTime baseTime, int index)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text
            .Split(['\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p => Regex.Split(p, @"\s{2,}"))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        if (parts.Length < 5) return null;

        var odds         = new List<decimal>();
        var firstOddsIdx = -1;

        for (var i = 0; i < parts.Length; i++)
        {
            if (TryParseDecimal(parts[i], out var v) && v is >= 1.01m and <= 49.99m)
            {
                if (odds.Count == 0) firstOddsIdx = i;
                odds.Add(v);
                if (odds.Count == 3) break;
            }
            else if (odds.Count is > 0 and < 3)
            {
                odds.Clear();
                firstOddsIdx = -1;
            }
        }

        if (odds.Count < 3 || firstOddsIdx < 2) return null;

        var meta      = parts.Take(firstOddsIdx).Where(p => !int.TryParse(p, out _)).ToArray();
        if (meta.Length < 2) return null;

        var startIdx    = 0;
        var scheduledAt = baseTime.AddHours(2);

        if (IsTimeToken(meta[0]))
        {
            scheduledAt = ParseTime(meta[0], baseTime);
            startIdx    = 1;
        }

        var teamParts = meta.Skip(startIdx).ToArray();
        if (teamParts.Length < 2) return null;

        var mid      = Math.Max(1, teamParts.Length / 2);
        var homeTeam = string.Join(" ", teamParts.Take(mid)).Trim();
        var awayTeam = string.Join(" ", teamParts.Skip(mid)).Trim();
        if (homeTeam.Length < 2 || awayTeam.Length < 2) return null;

        var league = DetectLeague(parts) ?? "ווינר";
        var isLive = scheduledAt <= DateTime.UtcNow;

        logger.LogDebug("[Text] #{Index} {Home} v {Away} {O1}/{OX}/{O2} @ {At:HH:mm}",
            index, homeTeam, awayTeam, odds[0], odds[1], odds[2], scheduledAt);

        return Build(index, homeTeam, awayTeam, league, scheduledAt, odds[0], odds[1], odds[2], isLive);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WinnerMatchDto Build(
        int index, string home, string away, string league,
        DateTime scheduledAt, decimal o1, decimal oX, decimal o2, bool isLive)
        => new(
            Guid.NewGuid(),
            $"scraped-{index:000}",
            home, away, league, scheduledAt,
            new OddsDto(o1, oX, o2),
            isLive ? "live" : "upcoming",
            isLive,
            DateTime.UtcNow);

    private static bool IsTimeToken(string s)
        => s.Length is >= 4 and <= 5
           && s.Contains(':')
           && TimeSpan.TryParse(s, out _);

    private static DateTime ParseTime(string? timeStr, DateTime baseTime)
    {
        if (timeStr is null || !TimeSpan.TryParse(timeStr, out var ts))
            return baseTime.AddHours(2);

        var candidate = baseTime.Date.Add(ts);
        if (candidate < baseTime.AddMinutes(-30))
            candidate = candidate.AddDays(1);
        return candidate;
    }

    private static string? DetectLeague(IEnumerable<string> tokens)
    {
        var text = string.Join(" ", tokens);
        foreach (var (kw, display) in LeagueKeywords)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return display;
        return null;
    }

    private static bool TryParseDecimal(string s, out decimal result)
    {
        var normalized = s.Replace(',', '.');
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out result);
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────

    private async Task LogSampleRowsAsync(IReadOnlyList<IElementHandle> rows, int maxRows)
    {
        var count = 0;
        foreach (var row in rows)
        {
            if (count >= maxRows) break;
            try
            {
                var cells = await row.QuerySelectorAllAsync("td");
                if (cells.Count > 0)
                {
                    var values = new List<string>();
                    foreach (var c in cells)
                        values.Add($"[{(await c.InnerTextAsync()).Trim()}]");
                    logger.LogInformation("Sample row {N} cells: {Cells}", count, string.Join(", ", values));
                }
                else
                {
                    var txt = (await row.InnerTextAsync()).Trim();
                    logger.LogInformation("Sample row {N} text: {Text}",
                        count, txt.Length > 200 ? txt[..200] + "…" : txt);
                }
                count++;
            }
            catch { /* ignore */ }
        }
    }

    private async Task LogPageDiagnosticsAsync(IPage page, string url)
    {
        try
        {
            var title   = await page.TitleAsync();
            var bodyText = await page.InnerTextAsync("body");
            logger.LogWarning(
                "Scraper found 0 matches on {Url}. " +
                "Page title: '{Title}', body chars: {Len}. " +
                "First 500 chars of body:\n{Body}",
                url, title, bodyText.Length,
                bodyText.Length > 500 ? bodyText[..500] : bodyText);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read page diagnostics");
        }
    }
}
