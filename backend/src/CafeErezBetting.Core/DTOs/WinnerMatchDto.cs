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
    DateTime LastUpdated
);

public record OddsDto(decimal Home, decimal Draw, decimal Away);

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

public record SyncStatusDto(bool Success, DateTime LastSync, string? Error);
