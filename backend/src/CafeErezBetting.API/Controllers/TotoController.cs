using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/toto")]
public class TotoController : ControllerBase
{
    // GET /api/toto/current — returns the current toto round with matches
    [HttpGet("current")]
    public IActionResult GetCurrentRound()
    {
        // TODO: Phase 3 — implement real scraping from Telesport/Livegames
        // For now: return mock round so frontend can be tested
        var round = new
        {
            roundId      = "toto-2026-001",
            roundNumber  = 1001,
            matches = new[]
            {
                new { id = "m1", homeTeam = "מכבי תל אביב",  awayTeam = "הפועל ב\"ש",    league = "ליגת העל" },
                new { id = "m2", homeTeam = "בית\"ר ירושלים", awayTeam = "מכבי חיפה",     league = "ליגת העל" },
                new { id = "m3", homeTeam = "Real Madrid",    awayTeam = "Barcelona",      league = "La Liga"  },
                new { id = "m4", homeTeam = "Man City",       awayTeam = "Arsenal",        league = "Premier League" },
                new { id = "m5", homeTeam = "Bayern",         awayTeam = "Dortmund",       league = "Bundesliga" },
                new { id = "m6", homeTeam = "PSG",            awayTeam = "Lyon",           league = "Ligue 1" },
                new { id = "m7", homeTeam = "Juventus",       awayTeam = "Inter",          league = "Serie A" },
                new { id = "m8", homeTeam = "Ajax",           awayTeam = "PSV",            league = "Eredivisie" },
                new { id = "m9", homeTeam = "הפועל ת\"א",     awayTeam = "מ.פ. תל אביב",  league = "ליגת העל" },
                new { id = "m10", homeTeam = "Atletico",      awayTeam = "Sevilla",        league = "La Liga" },
                new { id = "m11", homeTeam = "Chelsea",       awayTeam = "Liverpool",      league = "Premier League" },
                new { id = "m12", homeTeam = "Roma",          awayTeam = "Lazio",          league = "Serie A" },
                new { id = "m13", homeTeam = "Porto",         awayTeam = "Benfica",        league = "Primeira Liga" },
            }
        };

        return Ok(round);
    }

    // POST /api/toto/sync (admin) — manual refresh
    [HttpPost("sync")]
    public IActionResult ManualSync()
    {
        return Ok(new { message = "Toto sync triggered" });
    }
}
