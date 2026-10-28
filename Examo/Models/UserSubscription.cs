namespace Examo.Models;

// Paid plan ki validity (har successful payment par ek row)
public class UserSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string PlanKey { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public int? PaymentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
