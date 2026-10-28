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

// Frontend is data se PayU ko form POST karta hai
public class PayUCheckoutResult
{
    // https://test.payu.in/_payment  ya  https://secure.payu.in/_payment
    public string ActionUrl { get; set; } = string.Empty;

    // key, txnid, amount, productinfo, firstname, email, phone, surl, furl, hash
    public Dictionary<string, string> Fields { get; set; } = new();

    public string TxnId { get; set; } = string.Empty;

    public string PlanKey { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;
}

public class PayUCallbackResult
{
    public bool Success { get; set; }

    public string TxnId { get; set; } = string.Empty;

    // Browser ko yahin bhejna hai (frontend ka Subscription page)
    public string RedirectUrl { get; set; } = string.Empty;
}

public interface ISubscriptionService
{
    IReadOnlyList<SubscriptionPlan> Plans { get; }

    Task<SubscriptionStatus> GetStatusAsync(int userId);

    // backendBaseUrl: PayU is URL par surl/furl ke liye wapas aayega
    Task<PayUCheckoutResult> CreateCheckoutAsync(
        int userId,
        string planKey,
        string backendBaseUrl);

    // PayU ka response (surl / furl / webhook): hash check, phir plan chalu
    Task<PayUCallbackResult> HandlePayUResponseAsync(
        IDictionary<string, string> form);
}