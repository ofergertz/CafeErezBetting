namespace CafeErezBetting.Core.Entities;

public enum DebtCategory { Store, Winner, Toto, Lotto, Chance, Lucky777, Other }
public enum DebtStatus   { Open, Partial, Settled }

public class DebtRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DebtCategory Category { get; set; }
    public string? Description { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance => OriginalAmount - PaidAmount;
    public DebtStatus Status { get; set; } = DebtStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
