using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "admin")]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.Role, a.Action, a.IpAddress, a.CreatedAt))
            .ToListAsync();

        return Ok(new AuditLogsResponseDto(items, total, page, pageSize));
    }
}
