namespace CafeErezBetting.Core.DTOs;

public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal TotalDebt { get; set; }
    public int DebtCount { get; set; }
    public string DebtStatus { get; set; } = string.Empty; // e.g. "Open", "Partial", "Settled", "None"
}

public class CreateCustomerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class UpdateCustomerDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
}

public class DebtRecordDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateDebtDto
{
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; } = 0;
}

public class UpdateDebtDto
{
    public decimal PaidAmount { get; set; }
    public string? Description { get; set; }
}
