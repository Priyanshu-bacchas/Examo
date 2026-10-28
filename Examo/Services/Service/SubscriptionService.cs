using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Examo.Models;
using Examo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

// Free trial + Razorpay one-time payment.
//
// Render env vars:
//   Razorpay__KeyId          (Test: rzp_test_...)
//   Razorpay__KeySecret      (sirf backend me, kahin share mat karo)
//   Razorpay__WebhookSecret  (Razorpay dashboard me webhook banate waqt jo secret daalo)
public class SubscriptionService : ISubscriptionService
{
    private const int TrialDays = 2;

    private static readonly SubscriptionPlan[] AllPlans =
    {
        new() { Key = "monthly", Name = "Monthly Plan", AmountPaise = 4900,  Days = 30  },
        new() { Key = "yearly",  Name = "Yearly Plan",  AmountPaise = 19900, Days = 365 }
    };

    private readonly ExamoDbContext _db;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ExamoDbContext db,
        HttpClient http,
        IConfiguration config,
        IEmailService email,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _email = email;
        _logger = logger;
    }

    public IReadOnlyList<SubscriptionPlan> Plans => AllPlans;

    private static SubscriptionPlan? FindPlan(string? key) =>
        AllPlans.FirstOrDefault(p =>
            string.Equals(p.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    private string KeyId =>
        First(_config["Razorpay:KeyId"],
              Environment.GetEnvironmentVariable("RAZORPAY_KEY_ID"));

    private string KeySecret =>
        First(_config["Razorpay:KeySecret"],
              Environment.GetEnvironmentVariable("RAZORPAY_KEY_SECRET"));

    private string WebhookSecret =>
        First(_config["Razorpay:WebhookSecret"],
              Environment.GetEnvironmentVariable("RAZORPAY_WEBHOOK_SECRET"));

    private static string First(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return string.Empty;
    }

    // =====================================================
    // STATUS: Trial / Active / Expired
    // =====================================================
    public async Task<SubscriptionStatus> GetStatusAsync(int userId)
    {
        var student = await _db.Students
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (student == null)
            return new SubscriptionStatus { Status = "Expired" };

        // Admin ke liye hamesha poora access
        if (string.Equals(student.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return new SubscriptionStatus { Status = "Admin", HasAccess = true };

        var now = DateTime.UtcNow;

        // Trial pehli baar check hote hi shuru hota hai
        if (student.TrialEndsAt == null)
        {
            student.TrialEndsAt = now.AddDays(TrialDays);
            await _db.SaveChangesAsync();
        }

        var active = await _db.UserSubscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.EndsAt > now)
            .OrderByDescending(x => x.EndsAt)
            .FirstOrDefaultAsync();

        if (active != null)
        {
            return new SubscriptionStatus
            {
                Status = "Active",
                HasAccess = true,
                TrialEndsAt = student.TrialEndsAt,
                PlanKey = active.PlanKey,
                PlanName = FindPlan(active.PlanKey)?.Name ?? active.PlanKey,
                EndsAt = active.EndsAt,
                DaysLeft = DaysUntil(active.EndsAt, now)
            };
        }

        if (now < student.TrialEndsAt)
        {
            return new SubscriptionStatus
            {
                Status = "Trial",
                HasAccess = true,
                TrialEndsAt = student.TrialEndsAt,
                DaysLeft = DaysUntil(student.TrialEndsAt.Value, now)
            };
        }

        return new SubscriptionStatus
        {
            Status = "Expired",
            HasAccess = false,
            TrialEndsAt = student.TrialEndsAt
        };
    }

    private static int DaysUntil(DateTime end, DateTime now) =>
        Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

    // =====================================================
    // STEP 1: Razorpay Order banao (amount backend tay karta hai)
    // =====================================================
    public async Task<CreateOrderResult> CreateOrderAsync(
        int userId,
        string planKey)
    {
        var plan = FindPlan(planKey)
            ?? throw new ArgumentException("Please choose a valid plan.");

        if (string.IsNullOrWhiteSpace(KeyId) ||
            string.IsNullOrWhiteSpace(KeySecret))
        {
            throw new InvalidOperationException(
                "Payments are not set up yet. Please try again later.");
        }

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId)
            ?? throw new ArgumentException("User not found.");

        var body = JsonSerializer.Serialize(new
        {
            amount = plan.AmountPaise,
            currency = "INR",
            receipt = $"ex_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            notes = new { userId = userId.ToString(), plan = plan.Key }
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.razorpay.com/v1/orders");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}")));

        request.Content = new StringContent(
            body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseText;

        try
        {
            response = await _http.SendAsync(request);
            responseText = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay order request failed.");

            throw new InvalidOperationException(
                "Could not reach the payment service. Please try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Razorpay order failed: {Status} {Body}",
                (int)response.StatusCode,
                responseText);

            throw new InvalidOperationException(
                "Could not start the payment. Please try again.");
        }

        string orderId;

        using (var doc = JsonDocument.Parse(responseText))
        {
            orderId = doc.RootElement.GetProperty("id").GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException(
                "Could not start the payment. Please try again.");
        }

        _db.Payments.Add(new Payment
        {
            UserId = userId,
            PlanKey = plan.Key,
            AmountPaise = plan.AmountPaise,
            Currency = "INR",
            RazorpayOrderId = orderId,
            Status = "Created",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return new CreateOrderResult
        {
            KeyId = KeyId,
            OrderId = orderId,
            Amount = plan.AmountPaise,
            Currency = "INR",
            PlanKey = plan.Key,
            PlanName = plan.Name,
            Name = student.Name,
            Email = student.Email,
            Contact = student.MobileNumber
        };
    }

    // =====================================================
    // STEP 2: Checkout ke baad signature verify
    // =====================================================
    public async Task<SubscriptionStatus> VerifyPaymentAsync(
        int userId,
        string orderId,
        string paymentId,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(orderId) ||
            string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(signature))
        {
            throw new ArgumentException("Invalid payment details.");
        }

        var payment = await _db.Payments.FirstOrDefaultAsync(x =>
            x.RazorpayOrderId == orderId && x.UserId == userId)
            ?? throw new ArgumentException("Payment not found.");

        var expected = HmacHex(KeySecret, $"{orderId}|{paymentId}");

        if (!SafeEquals(expected, signature.Trim()))
        {
            _logger.LogWarning(
                "Razorpay signature mismatch for order {OrderId}", orderId);

            throw new InvalidOperationException(
                "Payment verification failed. If money was deducted, it will be confirmed automatically in a few minutes.");
        }

        await MarkPaidAsync(payment, paymentId);

        return await GetStatusAsync(userId);
    }

    // =====================================================
    // STEP 3: Webhook (tab band ho jaye tab bhi payment confirm)
    // =====================================================
    public async Task HandleWebhookAsync(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            throw new InvalidOperationException(
                "Webhook secret is not configured.");
        }

        var expected = HmacHex(WebhookSecret, rawBody);

        if (string.IsNullOrWhiteSpace(signature) ||
            !SafeEquals(expected, signature.Trim()))
        {
            throw new UnauthorizedAccessException("Invalid webhook signature.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var eventName = root.TryGetProperty("event", out var ev)
            ? ev.GetString()
            : null;

        if (eventName != "payment.captured" && eventName != "order.paid")
            return;

        if (!root.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("payment", out var paymentNode) ||
            !paymentNode.TryGetProperty("entity", out var entity))
        {
            return;
        }

        var status = entity.TryGetProperty("status", out var st)
            ? st.GetString()
            : null;

        var orderId = entity.TryGetProperty("order_id", out var oid)
            ? oid.GetString()
            : null;

        var paymentId = entity.TryGetProperty("id", out var pid)
            ? pid.GetString()
            : null;

        if (status != "captured" ||
            string.IsNullOrWhiteSpace(orderId) ||
            string.IsNullOrWhiteSpace(paymentId))
        {
            return;
        }

        var payment = await _db.Payments
            .FirstOrDefaultAsync(x => x.RazorpayOrderId == orderId);

        if (payment == null)
        {
            _logger.LogWarning(
                "Webhook for unknown order {OrderId}", orderId);

            return;
        }

        await MarkPaidAsync(payment, paymentId);
    }

    // =====================================================
    // Payment ko Paid karo, plan chalu karo, email bhejo.
    // Verify aur webhook dono aa jayein to bhi sirf ek baar chalta hai.
    // =====================================================
    private async Task MarkPaidAsync(Payment payment, string paymentId)
    {
        var now = DateTime.UtcNow;

        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Payments""
               SET ""Status"" = 'Paid',
                   ""RazorpayPaymentId"" = {paymentId},
                   ""PaidAt"" = {now}
               WHERE ""Id"" = {payment.Id} AND ""Status"" <> 'Paid'");

        // 0 = pehle hi ho chuka hai
        if (rows == 0)
            return;

        var plan = FindPlan(payment.PlanKey);

        if (plan == null)
        {
            _logger.LogError(
                "Paid payment {Id} has unknown plan {Plan}",
                payment.Id,
                payment.PlanKey);

            return;
        }

        // Pehle se chalu plan ho to uske baad se shuru (din waste nahi hote)
        var current = await _db.UserSubscriptions
            .Where(x => x.UserId == payment.UserId && x.EndsAt > now)
            .OrderByDescending(x => x.EndsAt)
            .FirstOrDefaultAsync();

        var start = current != null && current.EndsAt > now
            ? current.EndsAt
            : now;

        var end = start.AddDays(plan.Days);

        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = payment.UserId,
            PlanKey = plan.Key,
            StartsAt = start,
            EndsAt = end,
            PaymentId = payment.Id,
            CreatedAt = now
        });

        await _db.SaveChangesAsync();

        await SendPaymentEmailAsync(payment, plan, paymentId, end);
    }

    private async Task SendPaymentEmailAsync(
        Payment payment,
        SubscriptionPlan plan,
        string paymentId,
        DateTime endsAt)
    {
        try
        {
            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == payment.UserId);

            if (student == null || string.IsNullOrWhiteSpace(student.Email))
                return;

            var offset = _config.GetValue("Notifications:UtcOffsetMinutes", 330);

            var validTill = endsAt
                .AddMinutes(offset)
                .ToString("d MMM yyyy", CultureInfo.InvariantCulture);

            var amount = (payment.AmountPaise / 100m)
                .ToString("0.00", CultureInfo.InvariantCulture);

            var message =
                $"Thank you! We have received your payment for the {plan.Name}.\n\n" +
                $"Plan: {plan.Name}\n" +
                $"Amount paid: Rs {amount}\n" +
                $"Valid till: {validTill}\n" +
                $"Payment ID: {paymentId}\n" +
                $"Order ID: {payment.RazorpayOrderId}\n\n" +
                "You now have full access to Examo. Keep this email as your receipt.";

            await _email.SendNotificationEmailAsync(
                student.Email,
                student.Name,
                $"Payment received - Examo {plan.Name}",
                message);
        }
        catch (Exception ex)
        {
            // Email fail hone se payment / plan par koi asar nahi
            _logger.LogError(ex, "Payment email could not be sent.");
        }
    }

    private static string HmacHex(string secret, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));

        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(message)))
            .ToLowerInvariant();
    }

    private static bool SafeEquals(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var y = Encoding.UTF8.GetBytes(b.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(x, y);
    }
}