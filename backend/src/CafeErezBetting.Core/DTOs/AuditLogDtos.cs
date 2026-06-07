namespace CafeErezBetting.Core.DTOs;

public record AuditLogDto(
    Guid Id,
    string UserId,
    string Role,
    string Action,
    string IpAddress,
    DateTime CreatedAt
);

public record AuditLogsResponseDto(
    List<AuditLogDto> Items,
    int Total,
    int Page,
    int PageSize
);
