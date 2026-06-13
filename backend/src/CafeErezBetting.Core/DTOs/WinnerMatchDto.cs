using System.Text.Json.Serialization;

namespace CafeErezBetting.Core.DTOs;

public record WinnerMatchDto(
    Guid   Id,
    string ExternalId,
    string HomeTeam,
    string AwayTeam,
    string League,
    DateTime ScheduledAt,
    OddsDto Odds,
    string  Status,
    bool    IsLive,
    DateTime LastUpdated,
    string? BetType = null,
    string? Handicap = null,
    int? SubMarket = null,
    string? FormNumber = null,
    string? Score = null,
    string? Minute = null,
    string? BetCode = null,
    string? LeagueImageUrl = null
);

// JSON keys must match frontend: '1', 'X', '2'
// Draw is nullable — 1/2 markets (Asian handicap, over/under) have no X option.
public record OddsDto(
    [property: JsonPropertyName("1")] decimal  Home,
    [property: JsonPropertyName("X")] decimal? Draw,
    [property: JsonPropertyName("2")] decimal  Away
);

public record BetSlipItemDto(
    Guid   MatchId,
    string HomeTeam,
    string AwayTeam,
    string Pick,    // "1" | "X" | "2"
    decimal Odds
);

public record SubmitWinnerFormDto(
    List<BetSlipItemDto> Bets,
    decimal Stake,
    decimal TotalOdds,
    decimal PotentialWin,
    Guid?   CustomerId
);

public record FormSubmittedDto(Guid FormId, string Status);

public record SyncStatusDto(bool Success, DateTime LastSync, string? Error, bool IsMock = false);
