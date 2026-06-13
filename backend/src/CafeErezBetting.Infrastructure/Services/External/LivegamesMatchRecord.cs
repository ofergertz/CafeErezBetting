using System.Text.Json.Serialization;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Maps one record from the Livegames mobile API (/api/results?date=...).
/// One record = one complete match (unlike Telesport which has multiple markets per game).
/// </summary>
public record LivegamesMatchRecord(
    [property: JsonPropertyName("id")]                  int      Id,
    [property: JsonPropertyName("league_id")]           int      LeagueId,
    [property: JsonPropertyName("league_name_display")] string?  LeagueNameDisplay,
    [property: JsonPropertyName("league_image_url")]    string?  LeagueImageUrl,
    [property: JsonPropertyName("media_url")]           string?  MediaUrl,
    [property: JsonPropertyName("branch_id")]           int      BranchId,
    [property: JsonPropertyName("branch_name")]         string?  BranchName,
    [property: JsonPropertyName("p1_name")]             string?  P1Name,
    [property: JsonPropertyName("p2_name")]             string?  P2Name,
    [property: JsonPropertyName("p1score")]             int?     P1Score,
    [property: JsonPropertyName("p2score")]             int?     P2Score,
    [property: JsonPropertyName("status_id")]           int      StatusId,
    [property: JsonPropertyName("status_name_display")] string?  StatusNameDisplay,
    [property: JsonPropertyName("active")]              bool     Active,
    [property: JsonPropertyName("startDateISO")]        DateTime StartDateIso,
    [property: JsonPropertyName("win_rate_1")]          decimal? WinRate1,
    [property: JsonPropertyName("win_rate_x")]          decimal? WinRateX,
    [property: JsonPropertyName("win_rate_2")]          decimal? WinRate2,
    [property: JsonPropertyName("round_name")]          string?  RoundName,
    [property: JsonPropertyName("eventCode")]           string?  EventCode,
    [property: JsonPropertyName("winner")]              bool     Winner,
    [property: JsonPropertyName("toto")]                bool     Toto
);
