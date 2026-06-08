namespace CafeErezBetting.Core.DTOs;

/// <summary>
/// Lightweight summary returned by GET /api/forms — does NOT include the full Payload JSON.
/// Use GET /api/forms/{id} (admin only) to fetch the full form with payload.
/// </summary>
public record FormSummaryDto(
    Guid Id,
    string Type,
    string? CustomerName,
    string Status,
    DateTime SubmittedAt,
    DateTime? ReceivedAt,
    DateTime? ApprovedAt,
    DateTime? SentAt
);
