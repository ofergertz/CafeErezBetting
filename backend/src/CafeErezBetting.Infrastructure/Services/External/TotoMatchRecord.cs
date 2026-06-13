using System.Text.Json.Serialization;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// One match record from Telesport or Livegames Toto API (identical structure).
/// Endpoint: m.telesport.co.il/api/toto?date=... or m.livegames.co.il/api/toto?date=...
/// </summary>
public record TotoTelesportRecord(
    [property: JsonPropertyName("lineNum")]        int     LineNum,
    [property: JsonPropertyName("toto_id")]        int     TotoId,
    [property: JsonPropertyName("gameid")]         int     GameId,
    [property: JsonPropertyName("status_id")]      int     StatusId,
    [property: JsonPropertyName("status_display")] string? StatusDisplay,
    [property: JsonPropertyName("result")]         string? Result,
    [property: JsonPropertyName("score_1")]        int     Score1,
    [property: JsonPropertyName("score_2")]        int     Score2,
    [property: JsonPropertyName("league_name")]    string? LeagueName,
    [property: JsonPropertyName("league_image")]   string? LeagueImage,
    [property: JsonPropertyName("name_1")]         string? Name1,
    [property: JsonPropertyName("name_2")]         string? Name2,
    [property: JsonPropertyName("image_1")]        string? Image1,
    [property: JsonPropertyName("image_2")]        string? Image2,
    [property: JsonPropertyName("ismillionwin")]   bool    IsMillionWin,
    [property: JsonPropertyName("force_half")]     bool    ForceHalf
);

// ── Winner.co.il Toto API types ────────────────────────────────────────────────

public record TotoWinnerRow(
    [property: JsonPropertyName("rowNumber")]      int      RowNumber,
    [property: JsonPropertyName("day")]            string?  Day,
    [property: JsonPropertyName("time")]           string?  Time,
    [property: JsonPropertyName("eventStartTime")] DateTime EventStartTime,
    [property: JsonPropertyName("league")]         string?  League,
    [property: JsonPropertyName("teamA")]          string?  TeamA,
    [property: JsonPropertyName("teamB")]          string?  TeamB,
    [property: JsonPropertyName("status")]         string?  Status
);

public record TotoWinnerGame(
    [property: JsonPropertyName("gameType")]    int                  GameType,
    [property: JsonPropertyName("drawNumber")]  int?                 DrawNumber,
    [property: JsonPropertyName("drawId")]      int?                 DrawId,
    [property: JsonPropertyName("closeDateTime")] DateTime?          CloseDateTime,
    [property: JsonPropertyName("rows")]        List<TotoWinnerRow>? Rows
);

public record TotoWinnerResponse(
    [property: JsonPropertyName("games")]       List<TotoWinnerGame> Games
);
