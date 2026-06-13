using System.Text.Json.Serialization;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Nested game object from /api/winner - contains league and schedule info shared across all markets of the same game.
/// </summary>
public record LivegamesGameInfo(
    [property: JsonPropertyName("id")]               int      Id,
    [property: JsonPropertyName("startDate")]         DateTime StartDate,
    [property: JsonPropertyName("status_display")]    string?  StatusDisplay,
    [property: JsonPropertyName("branch_id")]         int      BranchId,
    [property: JsonPropertyName("branch_name")]       string?  BranchName,
    [property: JsonPropertyName("league_id")]         int      LeagueId,
    [property: JsonPropertyName("league_name")]       string?  LeagueName,
    [property: JsonPropertyName("league_image_url")]  string?  LeagueImageUrl,
    [property: JsonPropertyName("round_name")]        string?  RoundName,
    [property: JsonPropertyName("country_name")]      string?  CountryName,
    [property: JsonPropertyName("isCup")]             bool     IsCup
);

/// <summary>
/// Maps one betting-market record from /api/winner?date=...
/// One record = one market (multiple records share the same game.id).
/// </summary>
public record LivegamesWinnerRecord(
    [property: JsonPropertyName("uniqueid")]          string?  UniqueId,
    [property: JsonPropertyName("lineNum")]           int      LineNum,
    [property: JsonPropertyName("winner_id")]         int      WinnerId,
    [property: JsonPropertyName("type_name")]         string?  TypeName,
    [property: JsonPropertyName("status_id")]         int      StatusId,
    [property: JsonPropertyName("status_name")]       string?  StatusName,
    [property: JsonPropertyName("status_formatted")]  string?  StatusFormatted,
    [property: JsonPropertyName("activetoshow")]      bool     ActiveToShow,
    [property: JsonPropertyName("betCloseDate")]      DateTime BetCloseDate,
    [property: JsonPropertyName("rate_1")]            decimal? Rate1,
    [property: JsonPropertyName("rate_x")]            decimal? RateX,
    [property: JsonPropertyName("rate_2")]            decimal? Rate2,
    [property: JsonPropertyName("result_1")]          int?     Result1,
    [property: JsonPropertyName("result_2")]          int?     Result2,
    [property: JsonPropertyName("p1score")]           int?     P1Score,
    [property: JsonPropertyName("p2score")]           int?     P2Score,
    [property: JsonPropertyName("p1_name")]           string?  P1Name,
    [property: JsonPropertyName("p2_name")]           string?  P2Name,
    [property: JsonPropertyName("p1_image_url")]      string?  P1ImageUrl,
    [property: JsonPropertyName("p2_image_url")]      string?  P2ImageUrl,
    [property: JsonPropertyName("game")]              LivegamesGameInfo? Game
);
