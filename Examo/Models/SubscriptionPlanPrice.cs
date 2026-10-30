namespace Examo.Models;

// Admin ne jo plan price set kiya (agar row nahi hai to code ki default price chalti hai)
public class SubscriptionPlanPrice
{
    public int Id { get; set; }

    // "monthly" | "yearly"
    public string PlanKey { get; set; } = string.Empty;

    // Paise me (Rs 49 = 4900)
    public int AmountPaise { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}