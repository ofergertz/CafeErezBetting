using CafeErezBetting.Core.DTOs;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface ICustomerService
{
    Task<List<CustomerListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerListItemDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task<CustomerListItemDto> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<DebtRecordDto>> GetDebtsAsync(Guid customerId, CancellationToken ct = default);
    Task<DebtRecordDto> AddDebtAsync(Guid customerId, CreateDebtDto dto, CancellationToken ct = default);
    Task<DebtRecordDto> UpdateDebtAsync(Guid customerId, Guid debtId, UpdateDebtDto dto, CancellationToken ct = default);
    Task DeleteDebtAsync(Guid customerId, Guid debtId, CancellationToken ct = default);
}
