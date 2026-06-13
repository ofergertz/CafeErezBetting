namespace CafeErezBetting.Core.DTOs;

// ─── Toto ─────────────────────────────────────────────────────────────────────

public record TotoMatchDto(string Id, string HomeTeam, string AwayTeam, string League,
    DateTime? ScheduledAt = null, string? Result = null);

public record TotoRoundDto(string RoundId, int RoundNumber, List<TotoMatchDto> Matches, bool IsMock = false);

public record TotoColumnDto(Dictionary<string, string> Picks); // matchId → "1"|"X"|"2"

public record SubmitTotoFormDto(
    string RoundId,
    List<TotoColumnDto> Columns,
    Guid? CustomerId
);

// ─── Lotto ────────────────────────────────────────────────────────────────────

public record LottoRowDto(List<int> Numbers, int Strong);

public record SubmitLottoFormDto(
    List<LottoRowDto> Rows,
    decimal CostPerRow,
    Guid? CustomerId
);

// ─── Chance ──────────────────────────────────────────────────────────────────

public record ChanceRowDto(List<int> Numbers);

public record SubmitChanceFormDto(
    List<ChanceRowDto> Rows,
    decimal CostPerRow,
    Guid? CustomerId
);

// ─── 777 ─────────────────────────────────────────────────────────────────────

public record Lucky777RowDto(List<int> Numbers);

public record SubmitLucky777FormDto(
    List<Lucky777RowDto> Rows,
    decimal CostPerRow,
    Guid? CustomerId
);
