using System.Text.RegularExpressions;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.Infrastructure.Services;

public class CustomerService(AppDbContext db) : ICustomerService
{
    // ─── Validation helpers ───────────────────────────────────────────────────

    private static void ValidateIsraeliId(string id)
    {
        if (id.Length != 9 || !id.All(char.IsDigit))
            throw new ArgumentException("IdNumber must be a 9-digit Israeli ID number.");

        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            int digit = id[i] - '0';
            int val = digit * (i % 2 == 0 ? 1 : 2);
            if (val > 9) val -= 9;
            sum += val;
        }

        if (sum % 10 != 0)
            throw new ArgumentException("IdNumber is not a valid Israeli ID (failed Luhn check).");
    }

    private static readonly Regex PhoneRegex =
        new(@"^0(50|52|53|54|55|58|2|3|4|8|9)\d{7}$", RegexOptions.Compiled);

    private static void ValidatePhone(string phone)
    {
        if (!PhoneRegex.IsMatch(phone))
            throw new ArgumentException("Phone number is not a valid Israeli mobile/landline number.");
    }

    private static DebtStatus ComputeStatus(decimal originalAmount, decimal paidAmount) =>
        paidAmount == 0 ? DebtStatus.Open :
        paidAmount >= originalAmount ? DebtStatus.Settled :
        DebtStatus.Partial;

    private static string SummaryStatus(ICollection<DebtRecord> debts)
    {
        if (debts.Count == 0) return "None";
        if (debts.All(d => d.Status == DebtStatus.Settled)) return "Settled";
        if (debts.Any(d => d.Status == DebtStatus.Open)) return "Open";
        if (debts.Any(d => d.Status == DebtStatus.Partial)) return "Partial";
        return "Settled";
    }

    private static CustomerListItemDto MapCustomer(Customer c) => new()
    {
        Id         = c.Id,
        FirstName  = c.FirstName,
        LastName   = c.LastName,
        Phone      = c.Phone,
        IdNumber   = c.IdNumber,
        CreatedAt  = c.CreatedAt,
        TotalDebt  = c.Debts.Sum(d => d.OriginalAmount - d.PaidAmount),
        DebtCount  = c.Debts.Count,
        DebtStatus = SummaryStatus(c.Debts),
    };

    private static DebtRecordDto MapDebt(DebtRecord d) => new()
    {
        Id             = d.Id,
        CustomerId     = d.CustomerId,
        Category       = d.Category.ToString(),
        Description    = d.Description,
        OriginalAmount = d.OriginalAmount,
        PaidAmount     = d.PaidAmount,
        Balance        = d.OriginalAmount - d.PaidAmount,
        Status         = d.Status.ToString(),
        CreatedAt      = d.CreatedAt,
        UpdatedAt      = d.UpdatedAt,
    };

    // ─── Customers ────────────────────────────────────────────────────────────

    public async Task<List<CustomerListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var customers = await db.Customers
            .Include(c => c.Debts)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync(ct);

        return customers.Select(MapCustomer).ToList();
    }

    public async Task<CustomerListItemDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName))
            throw new ArgumentException("FirstName is required.");
        if (string.IsNullOrWhiteSpace(dto.LastName))
            throw new ArgumentException("LastName is required.");
        if (string.IsNullOrWhiteSpace(dto.IdNumber))
            throw new ArgumentException("IdNumber is required.");
        if (string.IsNullOrWhiteSpace(dto.Phone))
            throw new ArgumentException("Phone is required.");

        ValidateIsraeliId(dto.IdNumber);
        ValidatePhone(dto.Phone);

        if (await db.Customers.AnyAsync(c => c.IdNumber == dto.IdNumber, ct))
            throw new ArgumentException("A customer with this IdNumber already exists.");
        if (await db.Customers.AnyAsync(c => c.Phone == dto.Phone, ct))
            throw new ArgumentException("A customer with this Phone already exists.");

        var customer = new Customer
        {
            FirstName = dto.FirstName.Trim(),
            LastName  = dto.LastName.Trim(),
            IdNumber  = dto.IdNumber.Trim(),
            Phone     = dto.Phone.Trim(),
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return MapCustomer(customer);
    }

    public async Task<CustomerListItemDto> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .Include(c => c.Debts)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        if (dto.Phone is not null)
        {
            ValidatePhone(dto.Phone);
            if (await db.Customers.AnyAsync(c => c.Phone == dto.Phone && c.Id != id, ct))
                throw new ArgumentException("A customer with this Phone already exists.");
            customer.Phone = dto.Phone.Trim();
        }

        if (dto.FirstName is not null)
            customer.FirstName = dto.FirstName.Trim();
        if (dto.LastName is not null)
            customer.LastName = dto.LastName.Trim();

        await db.SaveChangesAsync(ct);
        return MapCustomer(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .Include(c => c.Debts)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        // Delete debts first, then customer (safe cascade)
        db.DebtRecords.RemoveRange(customer.Debts);
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
    }

    // ─── Debts ────────────────────────────────────────────────────────────────

    public async Task<List<DebtRecordDto>> GetDebtsAsync(Guid customerId, CancellationToken ct = default)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == customerId, ct);
        if (!exists) throw new KeyNotFoundException($"Customer {customerId} not found.");

        var debts = await db.DebtRecords
            .Where(d => d.CustomerId == customerId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return debts.Select(MapDebt).ToList();
    }

    public async Task<DebtRecordDto> AddDebtAsync(Guid customerId, CreateDebtDto dto, CancellationToken ct = default)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == customerId, ct);
        if (!exists) throw new KeyNotFoundException($"Customer {customerId} not found.");

        if (!Enum.TryParse<DebtCategory>(dto.Category, ignoreCase: true, out var category))
            throw new ArgumentException($"Invalid debt category: {dto.Category}.");
        if (dto.OriginalAmount <= 0)
            throw new ArgumentException("OriginalAmount must be positive.");
        if (dto.PaidAmount < 0)
            throw new ArgumentException("PaidAmount cannot be negative.");
        if (dto.PaidAmount > dto.OriginalAmount)
            throw new ArgumentException("paidAmount cannot exceed originalAmount");

        var debt = new DebtRecord
        {
            CustomerId     = customerId,
            Category       = category,
            Description    = dto.Description,
            OriginalAmount = dto.OriginalAmount,
            PaidAmount     = dto.PaidAmount,
            Status         = ComputeStatus(dto.OriginalAmount, dto.PaidAmount),
        };

        db.DebtRecords.Add(debt);
        await db.SaveChangesAsync(ct);
        return MapDebt(debt);
    }

    public async Task<DebtRecordDto> UpdateDebtAsync(Guid customerId, Guid debtId, UpdateDebtDto dto, CancellationToken ct = default)
    {
        var debt = await db.DebtRecords
            .FirstOrDefaultAsync(d => d.Id == debtId && d.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException($"Debt {debtId} not found for customer {customerId}.");

        if (dto.PaidAmount < 0)
            throw new ArgumentException("PaidAmount cannot be negative.");
        if (dto.PaidAmount > debt.OriginalAmount)
            throw new ArgumentException("paidAmount cannot exceed originalAmount");

        debt.PaidAmount   = dto.PaidAmount;
        debt.Status       = ComputeStatus(debt.OriginalAmount, dto.PaidAmount);
        debt.UpdatedAt    = DateTime.UtcNow;
        if (dto.Description is not null)
            debt.Description = dto.Description;

        await db.SaveChangesAsync(ct);
        return MapDebt(debt);
    }

    public async Task DeleteDebtAsync(Guid customerId, Guid debtId, CancellationToken ct = default)
    {
        var debt = await db.DebtRecords
            .FirstOrDefaultAsync(d => d.Id == debtId && d.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException($"Debt {debtId} not found for customer {customerId}.");

        db.DebtRecords.Remove(debt);
        await db.SaveChangesAsync(ct);
    }
}
