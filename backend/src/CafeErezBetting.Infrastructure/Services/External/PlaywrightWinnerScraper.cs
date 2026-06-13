using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Scrapes live winner odds from telesport.co.il or livegames.co.il using Playwright.
/// Both sites are JavaScript-rendered SPAs — a real browser is required to extract odds data.
/// Requires Playwright browser binaries: run `playwright install chromium` on the host or use the Docker image.
/// Tries URLs in order; on 0 results falls back to the next URL automatically.
/// Configurable via appsettings Scrapers:Winner section.
/// </summary>
public class PlaywrightWinnerScraper(
    IConfiguration config,
    ILogger<PlaywrightWinnerScraper> logger
)
{
    // Primary = telesport, secondary = livegames (automatic fallback)
    private static readonly string[] DefaultUrls =
    [
        "https://www.telesport.co.il/%D7%90%D7%96%D7%95%D7%A8%20%D7%95%D7%95%D7%99%D7%A0%D7%A8",
        "https://www.livegames.co.il/winnerpage.aspx",
    ];

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

    // Cells that are definitely not team names
    private static readonly HashSet<string> SkipWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Hebrew match statuses
        "הסתיים", "לא התחיל", "בשידור חי", "נדחה", "בוטל", "מושהה", "הפסקה",
        // JavaScript boolean fields
        "True", "False",
        // Table header labels
        "1X2",
    };

    // Known bet-type prefixes in Hebrew (from telesport/livegames bet type column)
    private static readonly string[] BetTypePrefixes =
    [
        "סך הכל קרנות", "סך הכל שערים", "מעל/מתחת שערים", "מעל/מתחת קרנות",
        "מעל/מתחת", "דאבל צ'אנס", "דאבל צאנס", "אסיאן הנדיקפ", "אסיאן",
        "מי ינצח", "תוצאה נכונה", "הפסקה ראשונה", "הפסקה שנייה",
    ];

    // ── Config ────────────────────────────────────────────────────────────────

    private string[] GetConfigUrls()
    {
        var urls = config.GetSection("Scrapers:Winner:Urls").Get<string[]>();
        return urls?.Length > 0 ? urls : DefaultUrls;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Scrapes all configured URLs in parallel using a single browser instance.
    /// Results are merged and deduplicated by (homeTeam, awayTeam).
    /// Use this for background sync to get the fastest and most complete data.
    /// </summary>
    public async Task<List<WinnerMatchDto>> ScrapeAllAsync(CancellationToken ct = default)
    {
        var urls = GetConfigUrls();
        logger.LogInformation("Playwright parallel scrape — {Count} sources", urls.Length);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchBrowserAsync(playwright);

        logger.LogInformation("Chromium launched — starting parallel page loads");

        var tasks = urls.Select((url, i) => ScrapeInContextAsync(browser, url, i, ct)).ToArray();
        var resultsBySource = await Task.WhenAll(tasks);

        // Merge and deduplicate by team pair (both sites show the same Winner matches)
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<WinnerMatchDto>();
        foreach (var sourceResults in resultsBySource)
        {
            foreach (var m in sourceResults)
            {
                var key = $"{m.HomeTeam.Trim().ToLowerInvariant()}|{m.AwayTeam.Trim().ToLowerInvariant()}";
                if (seen.Add(key)) merged.Add(m);
            }
        }

        logger.LogInformation("Parallel scrape complete: {Total} unique matches from {N} sources",
            merged.Count, resultsBySource.Length);
        return merged;
    }

    /// <summary>Scrapes a single configured source by index. Used by the admin source-picker dropdown.</summary>
    public async Task<List<WinnerMatchDto>> ScrapeAsync(int sourceIndex = 0, CancellationToken ct = default)
    {
        var urls = GetConfigUrls();
        var url  = sourceIndex < urls.Length ? urls[sourceIndex] : urls[0];

        logger.LogInformation("Playwright single-source scrape — source {Index}: {Url}", sourceIndex, url);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchBrowserAsync(playwright);

        logger.LogInformation("Chromium launched successfully");

        var matches = await ScrapeInContextAsync(browser, url, sourceIndex, ct);
        logger.LogInformation("Single-source scrape finished — {Count} matches from {Url}", matches.Count, url);
        return matches;
    }

    // ── Shared browser helpers ────────────────────────────────────────────────

    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        var chromiumPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");
        return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
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
    }

    private async Task<List<WinnerMatchDto>> ScrapeInContextAsync(
        IBrowser browser, string url, int sourceIndex, CancellationToken ct)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent         = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
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

        try
        {
            var matches = await TryScrapePageAsync(page, url);
            logger.LogInformation("Source {Index} ({Url}): {Count} matches", sourceIndex, url, matches.Count);
            return matches;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Per-URL navigation + extraction ──────────────────────────────────────

    private async Task<List<WinnerMatchDto>> TryScrapePageAsync(IPage page, string url)
    {
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout   = 30_000,
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

        await page.WaitForTimeoutAsync(2000);

        logger.LogInformation("Page loaded: title='{Title}', url='{Url}'",
            await page.TitleAsync(), page.Url);

        var matches = await ExtractMatchesAsync(page, url);

        if (matches.Count == 0)
            await TakeDebugScreenshotAsync(page);

        return matches;
    }

    // ── Row extraction ────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> ExtractMatchesAsync(IPage page, string url)
    {
        var results     = new List<WinnerMatchDto>();
        var now         = DateTime.UtcNow;
        var rowSelector = "tr";

        IReadOnlyList<IElementHandle> rows;

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
        await LogSampleRowsAsync(rows, maxRows: 3);

        var index = 0;
        var currentLeague = (string?)null;
        foreach (var row in rows)
        {
            try
            {
                // Detect league header rows by CSS class first
                var rowClass = (await row.GetAttributeAsync("class") ?? "").ToLower();
                if (rowClass.Contains("category") || rowClass.Contains("league") ||
                    rowClass.Contains("header") || rowClass.Contains("group") ||
                    rowClass.Contains("title") || rowClass.Contains("section"))
                {
                    var headerText = StripInvisibleChars((await row.InnerTextAsync()).Trim());
                    if (headerText.Length >= 4 && HasLetterChar(headerText))
                    {
                        currentLeague = headerText.Length > 100 ? headerText[..100] : headerText;
                        logger.LogDebug("League header (class '{Class}'): {League}", rowClass, currentLeague);
                    }
                    continue;
                }

                var cells = await row.QuerySelectorAllAsync("td");
                WinnerMatchDto? match = null;

                if (cells.Count >= 3)
                {
                    var cellTexts = new List<string>();
                    foreach (var cell in cells)
                        cellTexts.Add((await cell.InnerTextAsync()).Trim());

                    match = TryParseFromCells(cellTexts, now, index, currentLeague);

                    if (match is null)
                    {
                        // Heuristic: row with no odds and substantial text → league header
                        var candidate = TryExtractLeagueHeader(cellTexts);
                        if (candidate is not null)
                        {
                            currentLeague = candidate;
                            logger.LogDebug("League header (heuristic): {League}", currentLeague);
                        }
                    }
                }

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

            if (index >= 300) break;
        }

        if (results.Count == 0)
            await LogPageDiagnosticsAsync(page, url);

        return results;
    }

    // ── Cell-based parser ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses a list of &lt;td&gt; cell values from a telesport.co.il / livegames.co.il row.
    ///
    /// Row structure (RTL order):
    ///   [.223 (6)] [★] [status/time] [HomeTeam - AwayTeam] [S,D] [odd1] [oddX] [odd2] [score]
    ///
    /// Filters applied per cell:
    ///   - Pure integers      → match/round numbers, skip
    ///   - ^\.\d+             → dot-prefixed Winner form number (.223, .4), skip
    ///   - SkipWords          → status text (הסתיים) and JS booleans (True/False), skip
    ///   - ^\([^)]+\)$        → parenthetical only "(6)", "(7)", skip
    ///   - All-ASCII ≤5 chars → bet type codes (S,D), skip
    ///   - IsTimeToken        → timeCell (HH:mm)
    ///   - IsScoreToken       → score like "2:3", skip
    ///   - Any decimal        → odds if 1.01–49.99, else skip — never a team name
    ///   - No letter chars    → skip (symbols, dash-only, numbers with punctuation)
    ///   - Contains " - "     → split as "HomeTeam - AwayTeam"
    /// </summary>
    private WinnerMatchDto? TryParseFromCells(List<string> cells, DateTime baseTime, int index, string? overrideLeague = null)
    {
        var odds      = new List<decimal>();
        var textCells = new List<string>();
        var timeCell  = (string?)null;
        string? detectedBetType   = null;
        string? detectedHandicap  = null;
        int?    detectedSubMarket  = null;
        string? detectedFormNumber = null;
        string? detectedScore      = null;
        string? detectedMinute     = null;
        string? detectedBetCode    = null;

        foreach (var raw in cells)
        {
            // Strip invisible Unicode directional/format marks that prefix RTL cells
            var cell = StripInvisibleChars(raw.Trim());
            if (string.IsNullOrWhiteSpace(cell)) continue;

            // Pure integer → match/round number, skip
            if (int.TryParse(cell, out _)) continue;

            // ".223" or ".223 (6)" → Winner form number; extract sub-market count from "(N)" if present
            if (Regex.IsMatch(cell, @"^\.\d+"))
            {
                var numMatch = Regex.Match(cell, @"^\.(\d+)");
                if (numMatch.Success) detectedFormNumber = numMatch.Groups[1].Value;
                var subMatch = Regex.Match(cell, @"\((\d+)\)");
                if (subMatch.Success && int.TryParse(subMatch.Groups[1].Value, out var n))
                    detectedSubMarket = n;
                continue;
            }

            // Known status text / JS boolean fields
            if (SkipWords.Contains(cell)) continue;

            // Parenthetical-only like "(6)", "(7)" → market count, skip
            if (Regex.IsMatch(cell, @"^\([^)]+\)$")) continue;

            // All-ASCII short codes: "S,D", "1X", "X2", "12", "SD" etc. — capture as bet code
            if (Regex.IsMatch(cell, @"^[A-Z0-9,./]+$") && cell.Length <= 5)
            {
                detectedBetCode = cell;
                continue;
            }

            // HH:mm or H:mm → time  (must precede IsScoreToken — "18:30" would pass both)
            if (IsTimeToken(cell) && timeCell is null)
            {
                timeCell = cell;
                continue;
            }

            // Score token like "2:3" → skip
            if (IsScoreToken(cell)) continue;

            // Any decimal → odds (if in range) or skip — never a team name
            if (TryParseDecimal(cell, out var d))
            {
                if (d is >= 1.01m and <= 49.99m)
                    odds.Add(d);
                continue;
            }

            // Live score like "0-0" or "2-1" (dash-separated integers)
            if (IsMatchScore(cell, out var matchScore))
            {
                detectedScore = matchScore;
                continue;
            }

            // Match minute like "'3" or "45'" (apostrophe + integer)
            if (IsMinuteToken(cell, out var matchMinute))
            {
                detectedMinute = matchMinute;
                continue;
            }

            // Must contain at least one letter
            if (!HasLetterChar(cell)) continue;

            // "HomeTeam - AwayTeam" in one cell → split, stripping any leading bet-type prefix
            if (cell.Contains(" - "))
            {
                var sep   = cell.IndexOf(" - ", StringComparison.Ordinal);
                var raw1  = cell[..sep].Trim();
                var part2 = CleanTeamName(cell[(sep + 3)..].Trim());

                // Extract bet type prefix from the home-team segment if present
                var (part1, betType, handicap) = StripBetTypePrefix(raw1);
                part1 = CleanTeamName(part1);
                if (betType is not null) detectedBetType  = betType;
                if (handicap is not null) detectedHandicap = handicap;

                if (part1.Length >= 2) textCells.Add(part1);
                if (part2.Length >= 2) textCells.Add(part2);
                continue;
            }

            if (cell.Length >= 2)
                textCells.Add(CleanTeamName(cell));
        }

        if (odds.Count < 3 || textCells.Count < 2) return null;

        var scheduledAt = ParseTime(timeCell, baseTime);
        var league      = overrideLeague ?? DetectLeague(textCells) ?? "ווינר";
        var teamCells   = textCells.Where(t => t != league).ToList();
        if (teamCells.Count < 2) teamCells = textCells;

        var homeTeam = teamCells[0].Trim();
        var awayTeam = teamCells[^1].Trim();
        if (homeTeam.Length < 2 || awayTeam.Length < 2 || homeTeam == awayTeam) return null;

        var isLive = scheduledAt <= DateTime.UtcNow;

        logger.LogDebug("[Cell] #{Index} {Home} v {Away} {O1}/{OX}/{O2} @ {At:HH:mm}",
            index, homeTeam, awayTeam, odds[0], odds[1], odds[2], scheduledAt);

        return Build(index, homeTeam, awayTeam, league, scheduledAt,
            odds[0], odds[1], odds[2], isLive, detectedBetType, detectedHandicap, detectedSubMarket,
            detectedFormNumber, detectedScore, detectedMinute, detectedBetCode);
    }

    // ── Text-based parser (fallback for div/span layouts) ─────────────────────

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

        var meta = parts.Take(firstOddsIdx)
            .Where(p => !int.TryParse(p, out _)
                     && !Regex.IsMatch(p, @"^\.\d+")
                     && !SkipWords.Contains(p)
                     && HasLetterChar(p))
            .ToArray();

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

        return Build(index, homeTeam, awayTeam, league, scheduledAt,
            odds[0], odds[1], odds[2], isLive);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Heuristic: a row with no odds and substantial Hebrew text is a league header row.
    /// </summary>
    private static string? TryExtractLeagueHeader(List<string> cells)
    {
        // Must not contain odds values
        if (cells.Any(c =>
        {
            var s = StripInvisibleChars(c.Trim());
            return TryParseDecimal(s, out var d) && d is >= 1.01m and <= 49.99m;
        })) return null;

        var parts = cells
            .Select(c => StripInvisibleChars(c.Trim()))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Where(c => !int.TryParse(c, out _))
            .Where(c => !Regex.IsMatch(c, @"^\.\d+"))
            .Where(c => !SkipWords.Contains(c))
            .Where(c => !Regex.IsMatch(c, @"^\([^)]+\)$"))
            .Where(c => !Regex.IsMatch(c, @"^[A-Z0-9,./]+$") || c.Length > 5)
            .ToList();

        var combined = string.Join(" ", parts).Trim();
        var letterCount = combined.Count(char.IsLetter);
        if (letterCount < 4) return null;

        return combined.Length > 120 ? combined[..120] : combined;
    }

    private static WinnerMatchDto Build(
        int index, string home, string away, string league,
        DateTime scheduledAt, decimal o1, decimal oX, decimal o2, bool isLive,
        string? betType = null, string? handicap = null, int? subMarket = null,
        string? formNumber = null, string? score = null, string? minute = null, string? betCode = null)
        => new(
            Guid.NewGuid(),
            $"scraped-{index:000}",
            home, away, league, scheduledAt,
            new OddsDto(o1, oX, o2),
            isLive ? "live" : "upcoming",
            isLive,
            DateTime.UtcNow,
            betType,
            handicap,
            subMarket,
            formNumber,
            score,
            minute,
            betCode);

    /// <summary>
    /// Strips invisible Unicode directional and format characters that prefix RTL site content.
    /// These prevent regex like ^\.\d+ from matching cells like U+200F + ".13 (7)".
    /// </summary>
    private static string StripInvisibleChars(string s)
        => Regex.Replace(s, @"[​-‏‪-‮﻿­]", "").Trim();

    /// <summary>
    /// If the string starts with a known bet-type prefix (e.g. "סך הכל קרנות (9-11) TeamA"),
    /// returns (teamPart, betType, handicap). Otherwise returns (original, null, null).
    /// </summary>
    private static (string Team, string? BetType, string? Handicap) StripBetTypePrefix(string s)
    {
        foreach (var prefix in BetTypePrefixes)
        {
            if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = s[prefix.Length..].Trim();

            // Try to extract handicap like "(9-11)" or "(+1.5)"
            var handicapMatch = Regex.Match(rest, @"^\(([^)]+)\)\s*");
            string? handicap = null;
            if (handicapMatch.Success)
            {
                handicap = handicapMatch.Groups[1].Value;
                rest     = rest[handicapMatch.Length..].Trim();
            }

            return (rest.Length >= 2 ? rest : s, prefix, handicap);
        }
        return (s, null, null);
    }

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

    // "0-0", "2-1", "10-3" — live match score with dash separator
    private static bool IsMatchScore(string s, out string score)
    {
        score = "";
        var idx = s.IndexOf('-');
        if (idx < 1 || idx >= s.Length - 1) return false;
        if (!int.TryParse(s.AsSpan(0, idx), out _)) return false;
        if (!int.TryParse(s.AsSpan(idx + 1), out _)) return false;
        score = s;
        return true;
    }

    // "'3", "'45", "3'", "45'" — current match minute
    private static bool IsMinuteToken(string s, out string minute)
    {
        minute = "";
        if (s.Length >= 2 && s[0] == '\'')
        {
            var rest = s[1..].Trim();
            if (int.TryParse(rest, out _)) { minute = rest; return true; }
        }
        if (s.Length >= 2 && s[^1] == '\'')
        {
            var rest = s[..^1].Trim();
            if (int.TryParse(rest, out _)) { minute = rest; return true; }
        }
        return false;
    }

    private static bool HasLetterChar(string s)
    {
        foreach (var c in s)
            if (char.IsLetter(c)) return true;
        return false;
    }

    /// <summary>Strips trailing handicap notation like "(+1)", "(-1.5)", "(2.5)".</summary>
    private static string CleanTeamName(string s)
    {
        var cleaned = Regex.Replace(s, @"\s*\([^)]*\)\s*$", "").Trim();
        return cleaned.Length >= 2 ? cleaned : s;
    }

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
            var title    = await page.TitleAsync();
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

    private async Task TakeDebugScreenshotAsync(IPage page)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(),
                $"scraper-debug-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            logger.LogWarning("Debug screenshot saved → {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not save debug screenshot");
        }
    }
}
