using System.Text.Json;

namespace CafeErezBetting.Core.Entities;

public enum FormType   { Winner, Toto, Lotto, Chance, Lucky777 }
public enum FormStatus { Received = 0, Approved = 1, Sent = 2, Pending = 3 }

public class BettingForm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FormType Type { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Form-specific data stored as JSON — never null, never deleted.</summary>
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");

    public FormStatus Status { get; set; } = FormStatus.Pending;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }   // admin dismissed popup
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
