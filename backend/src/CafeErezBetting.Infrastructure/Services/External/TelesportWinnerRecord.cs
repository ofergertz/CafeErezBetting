using System.Text.Json.Serialization;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Maps one row from the Telesport Winner JSON API (flat array).
/// A single physical match appears as multiple rows — one per betting market (type_name).
/// </summary>
public record TelesportWinnerRecord(
    [property: JsonPropertyName("winner_id")]           int      WinnerId,
    [property: JsonPropertyName("gameid")]              int      GameId,
    [property: JsonPropertyName("lineNum")]             int      LineNum,
    [property: JsonPropertyName("status_id")]           int      StatusId,
    [property: JsonPropertyName("status_name_display")] string?  StatusNameDisplay,
    [property: JsonPropertyName("type_name")]           string?  TypeName,
    [property: JsonPropertyName("result_1")]            int?     Result1,
    [property: JsonPropertyName("result_x")]            int?     ResultX,
    [property: JsonPropertyName("result_2")]            int?     Result2,
    [property: JsonPropertyName("res_12_or_x")]         int      Res12OrX,
    [property: JsonPropertyName("rate_1")]              decimal? Rate1,
    [property: JsonPropertyName("rate_x")]              decimal? RateX,
    [property: JsonPropertyName("rate_2")]              decimal? Rate2,
    [property: JsonPropertyName("league_name_display")] string?  LeagueNameDisplay,
    [property: JsonPropertyName("league_image_url")]    string?  LeagueImageUrl,
    [property: JsonPropertyName("name_1")]              string?  Name1,
    [property: JsonPropertyName("name_2")]              string?  Name2,
    [property: JsonPropertyName("betCloseDate")]        DateTime BetCloseDate,
    [property: JsonPropertyName("isSingle")]            bool     IsSingle,
    [property: JsonPropertyName("isDouble")]            bool     IsDouble,
    [property: JsonPropertyName("activetoshow")]        bool     ActiveToShow,
    [property: JsonPropertyName("branch_id")]           int      BranchId
);
