namespace CafeErezBetting.Core.Entities;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;   // ת"ז (unique)
    public string Phone { get; set; } = string.Empty;      // unique — used for OTP
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DebtRecord> Debts { get; set; } = new List<DebtRecord>();
    public ICollection<BettingForm> Forms { get; set; } = new List<BettingForm>();
}
