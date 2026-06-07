namespace CafeErezBetting.Core.Entities;

public enum MatchStatus { Upcoming, Live, Finished, Suspended }

public class WinnerMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public decimal Odds1 { get; set; }
    public decimal OddsX { get; set; }
    public decimal Odds2 { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Upcoming;
    public bool IsLive { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
