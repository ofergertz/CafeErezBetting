using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface IFormsService
{
    Task<FormSubmittedDto> SubmitWinnerFormAsync(SubmitWinnerFormDto dto, CancellationToken ct = default);
    Task<List<FormSummaryDto>> GetAllFormsAsync(string? status, string? type, DateOnly? date, CancellationToken ct = default);
    Task<BettingForm?> GetFormByIdAsync(Guid formId, CancellationToken ct = default);
    Task UpdateFormStatusAsync(Guid formId, FormStatus newStatus, CancellationToken ct = default);
}
