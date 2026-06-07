using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.Infrastructure.Services;

public class FormsService(
    AppDbContext db,
    IMatchNotificationService notifier
) : IFormsService
{
    public async Task<FormSubmittedDto> SubmitWinnerFormAsync(SubmitWinnerFormDto dto, CancellationToken ct = default)
    {
        var form = new BettingForm
        {
            Type       = FormType.Winner,
            CustomerId = dto.CustomerId,
            Payload    = JsonDocument.Parse(JsonSerializer.Serialize(dto)),
            Status     = FormStatus.Received,
        };

        db.BettingForms.Add(form);
        await db.SaveChangesAsync(ct);

        // resolve customer display name
        string customerName = "אנונימי";
        if (dto.CustomerId.HasValue)
        {
            var customer = await db.Customers.FindAsync([dto.CustomerId.Value], ct);
            if (customer is not null)
                customerName = $"{customer.FirstName} {customer.LastName}";
        }

        // push SignalR notification to all admin sessions
        await notifier.NotifyNewFormAsync(form.Id, "winner", customerName, ct);

        return new FormSubmittedDto(form.Id, form.Status.ToString().ToLower());
    }

    public async Task<List<BettingForm>> GetAllFormsAsync(string? status, string? type, DateOnly? date, CancellationToken ct = default)
    {
        var query = db.BettingForms.Include(f => f.Customer).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<FormStatus>(status, true, out var s))
            query = query.Where(f => f.Status == s);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<FormType>(type, true, out var t))
            query = query.Where(f => f.Type == t);

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end   = start.AddDays(1);
            query = query.Where(f => f.SubmittedAt >= start && f.SubmittedAt < end);
        }

        return await query.OrderByDescending(f => f.SubmittedAt).ToListAsync(ct);
    }

    public async Task UpdateFormStatusAsync(Guid formId, FormStatus newStatus, CancellationToken ct = default)
    {
        var form = await db.BettingForms.FindAsync([formId], ct)
            ?? throw new KeyNotFoundException($"Form {formId} not found");

        if ((int)newStatus <= (int)form.Status)
            throw new InvalidOperationException(
                $"Cannot move form status from {form.Status} to {newStatus}: status transitions must be forward-only (Received=0 → Approved=1 → Sent=2)");

        form.Status = newStatus;

        switch (newStatus)
        {
            case FormStatus.Received: form.ReceivedAt = DateTime.UtcNow; break;
            case FormStatus.Approved: form.ApprovedAt = DateTime.UtcNow; break;
            case FormStatus.Sent:     form.SentAt     = DateTime.UtcNow; break;
        }

        await db.SaveChangesAsync(ct);
        await notifier.NotifyFormStatusChangedAsync(formId, newStatus.ToString().ToLower(), ct);
    }
}
