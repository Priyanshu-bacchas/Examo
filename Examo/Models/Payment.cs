namespace Examo.Models;

// Razorpay se hui har payment ka record (Created -> Paid)
public class Payment
{
    public int Id { get; set; }

    public int UserId { get; set; }

    // "monthly" | "yearly"
    public string PlanKey { get; set; } = string.Empty;

    // Paise me (Rs 49 = 4900)
    public int AmountPaise { get; set; }

    public string Currency { get; set; } = "INR";

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string? RazorpayPaymentId { get; set; }

    // Created | Paid
    public string Status { get; set; } = "Created";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
