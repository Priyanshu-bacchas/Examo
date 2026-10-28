namespace Examo.Services.Interfaces;

public class SubscriptionPlan
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // Paise me
    public int AmountPaise { get; set; }

    public int Days { get; set; }
}

public class SubscriptionStatus
{
    // Trial | Active | Expired | Admin
    public string Status { get; set; } = "Expired";

    // false ho to student ke liye sirf Help aur Subscription khule rehte hain
    public bool HasAccess { get; set; }

    public DateTime? TrialEndsAt { get; set; }

    public string? PlanKey { get; set; }

    public string? PlanName { get; set; }

    public DateTime? EndsAt { get; set; }

    // Trial / Active me kitne din bache (bacha hua time upar ki taraf round)
    public int DaysLeft { get; set; }
}

public class CreateOrderResult
{
    public string KeyId { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public int Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string PlanKey { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Contact { get; set; }
}

public interface ISubscriptionService
{
    IReadOnlyList<SubscriptionPlan> Plans { get; }

    Task<SubscriptionStatus> GetStatusAsync(int userId);

    Task<CreateOrderResult> CreateOrderAsync(int userId, string planKey);

    Task<SubscriptionStatus> VerifyPaymentAsync(
        int userId,
        string orderId,
        string paymentId,
        string signature);

    Task HandleWebhookAsync(string rawBody, string? signature);
}
