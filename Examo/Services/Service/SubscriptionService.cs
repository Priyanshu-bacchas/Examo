using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Examo.Models;
using Examo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Examo.Services;

// Free trial + PayU one-time payment (PayU Hosted Checkout).
//
// Render env vars (backend):
//   PayU__Key          PayU Merchant Key   (test ya live)
//   PayU__Salt         PayU Merchant Salt  (sirf backend me, kahin share mat karo)
//   PayU__Mode         test  |  live       (khaali ho to test)
//   PayU__BackendUrl   https://tera-backend.onrender.com   (PayU is par wapas aata hai)
//   App__FrontendUrl   https://tera-frontend.onrender.com  (payment ke baad user yahan aata hai)
//
// NOTE: Payments table ke purane column naam (RazorpayOrderId / RazorpayPaymentId)
// waise hi rakhe hain taaki database migration na karni pade.
//   RazorpayOrderId   = PayU txnid
//   RazorpayPaymentId = PayU mihpayid
public class SubscriptionService : ISubscriptionService
{
    // Admin ne trial ke din set nahi kiye to ye chalta hai
    public const int DefaultTrialDays = 2;

    private const string TrialSettingKey = "trial-days";
    private const string TrialCacheKey = "trial-days";

    private const string PriceCacheKey = "plan-prices";

    // Default prices. Asli price admin ke Payments > Plan Pricing se aati hai
    // (PlanPrices table); table me row na ho to ye default chalti hai.
    private static readonly SubscriptionPlan[] AllPlans =
    {
        new() { Key = "monthly", Name = "Monthly Plan", AmountPaise = 4900,  Days = 30  },
        new() { Key = "yearly",  Name = "Yearly Plan",  AmountPaise = 19900, Days = 365 }
    };

    private readonly ExamoDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ExamoDbContext db,
        HttpClient http,
        IConfiguration config,
        IEmailService email,
        IMemoryCache cache,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _config = config;
        _email = email;
        _cache = cache;
        _logger = logger;
    }

    // Naam / din ke liye (price yahan se kabhi nahi lete)
    private static SubscriptionPlan? FindPlan(string? key) =>
        AllPlans.FirstOrDefault(p =>
            string.Equals(p.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    // ---------------- Plan prices (admin set karta hai) ----------------

    public async Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync()
    {
        var prices = await GetPriceOverridesAsync();

        return AllPlans
            .Select(p => new SubscriptionPlan
            {
                Key = p.Key,
                Name = p.Name,
                Days = p.Days,
                AmountPaise = prices.TryGetValue(p.Key, out var paise)
                    ? paise
                    : p.AmountPaise
            })
            .ToList();
    }

    private async Task<Dictionary<string, int>> GetPriceOverridesAsync()
    {
        if (_cache.TryGetValue(PriceCacheKey, out Dictionary<string, int>? cached) &&
            cached != null)
        {
            return cached;
        }

        try
        {
            var rows = await _db.PlanPrices.AsNoTracking().ToListAsync();

            var map = rows
                .Where(x => x.AmountPaise >= 100)
                .GroupBy(x => x.PlanKey.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().AmountPaise);

            _cache.Set(PriceCacheKey, map, TimeSpan.FromMinutes(2));

            return map;
        }
        catch (Exception ex)
        {
            // Table abhi na ho to default price se kaam chalta rahe
            _logger.LogError(ex, "Plan prices could not be loaded.");

            return new Dictionary<string, int>();
        }
    }

    public async Task SetPlanPriceAsync(
        string planKey,
        int amountPaise,
        string? updatedBy)
    {
        var plan = FindPlan(planKey)
            ?? throw new ArgumentException("Please choose a valid plan.");

        // Payment gateway ka minimum Rs 1 hai
        if (amountPaise < 100 || amountPaise > 10_000_000)
        {
            throw new ArgumentException(
                "Price must be between Rs 1 and Rs 1,00,000.");
        }

        var row = await _db.PlanPrices
            .FirstOrDefaultAsync(x => x.PlanKey == plan.Key);

        if (row == null)
        {
            _db.PlanPrices.Add(new SubscriptionPlanPrice
            {
                PlanKey = plan.Key,
                AmountPaise = amountPaise,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }
        else
        {
            row.AmountPaise = amountPaise;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }

        await _db.SaveChangesAsync();

        _cache.Remove(PriceCacheKey);
    }

    // ---------------- Free trial (admin set karta hai) ----------------

    public async Task<int> GetTrialDaysAsync()
    {
        if (_cache.TryGetValue(TrialCacheKey, out int cached))
            return cached;

        try
        {
            var row = await _db.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == TrialSettingKey);

            var days = row != null &&
                       int.TryParse(row.Value, out var parsed) &&
                       parsed >= 0 && parsed <= 90
                ? parsed
                : DefaultTrialDays;

            _cache.Set(TrialCacheKey, days, TimeSpan.FromMinutes(2));

            return days;
        }
        catch (Exception ex)
        {
            // Table abhi na ho to default se kaam chalta rahe
            _logger.LogError(ex, "Trial days could not be loaded.");

            return DefaultTrialDays;
        }
    }

    public async Task SetTrialDaysAsync(int days, string? updatedBy)
    {
        if (days < 0 || days > 90)
        {
            throw new ArgumentException(
                "Trial must be between 0 and 90 days.");
        }

        var row = await _db.AppSettings
            .FirstOrDefaultAsync(x => x.Key == TrialSettingKey);

        if (row == null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = TrialSettingKey,
                Value = days.ToString(CultureInfo.InvariantCulture),
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }
        else
        {
            row.Value = days.ToString(CultureInfo.InvariantCulture);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }

        await _db.SaveChangesAsync();

        _cache.Remove(TrialCacheKey);
    }

    // ---------------- Config ----------------
    private string MerchantKey =>
        First(_config["PayU:Key"],
              Environment.GetEnvironmentVariable("PAYU_KEY"));

    private string MerchantSalt =>
        First(_config["PayU:Salt"],
              Environment.GetEnvironmentVariable("PAYU_SALT"));

    private bool IsLive =>
        string.Equals(
            First(_config["PayU:Mode"],
                  Environment.GetEnvironmentVariable("PAYU_MODE")),
            "live",
            StringComparison.OrdinalIgnoreCase);

    private string ActionUrl =>
        IsLive
            ? "https://secure.payu.in/_payment"
            : "https://test.payu.in/_payment";

    private string ConfiguredBackendUrl =>
        First(_config["PayU:BackendUrl"]).TrimEnd('/');

    // Frontend ke possible URLs (App__FrontendUrl + Cors__AllowedOrigins)
    private List<string> ConfiguredFrontends()
    {
        var list = new List<string>();

        var app = First(_config["App:FrontendUrl"]).TrimEnd('/');

        if (app.Length > 0)
            list.Add(app);

        list.AddRange(
            (_config["Cors:AllowedOrigins"] ?? "")
                .Split(new[] { ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(x => x.TrimEnd('/'))
                .Where(x => x.Length > 0));

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Config me jo pehla URL hai, warna local
    private string DefaultFrontendUrl =>
        ConfiguredFrontends().FirstOrDefault() ?? "http://localhost:5173";

    // User jis website se payment shuru karta hai, wahi wapas aana chahiye
    // (live site se live, local se local). Galat / anjaan URL par kabhi nahi bhejte.
    private string ResolveFrontendUrl(string? requested)
    {
        var candidate = (requested ?? "").Trim();

        if (candidate.Length > 0 &&
            Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps ||
             uri.Scheme == Uri.UriSchemeHttp))
        {
            var origin = $"{uri.Scheme}://{uri.Authority}";

            var configured = ConfiguredFrontends();

            var isLocal =
                uri.Host == "localhost" ||
                uri.Host == "127.0.0.1";

            var isConfigured = configured.Contains(
                origin,
                StringComparer.OrdinalIgnoreCase);

            // Kuch configure nahi hai to sirf https wali site maano
            var noConfigHttps =
                configured.Count == 0 &&
                uri.Scheme == Uri.UriSchemeHttps;

            if (isLocal || isConfigured || noConfigHttps)
                return origin;
        }

        return DefaultFrontendUrl;
    }

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
            var trialDays = await GetTrialDaysAsync();

            student.TrialStartsAt = now;
            student.TrialEndsAt = now.AddDays(trialDays);
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
    // STEP 1: PayU checkout ka data banao (amount backend tay karta hai)
    // =====================================================
    public async Task<PayUCheckoutResult> CreateCheckoutAsync(
        int userId,
        string planKey,
        string backendBaseUrl,
        string? frontendOrigin)
    {
        // Price yahan DB (admin ki set ki hui) se aati hai
        var plan = (await GetPlansAsync()).FirstOrDefault(p =>
                string.Equals(p.Key, planKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Please choose a valid plan.");

        if (string.IsNullOrWhiteSpace(MerchantKey) ||
            string.IsNullOrWhiteSpace(MerchantSalt))
        {
            throw new InvalidOperationException(
                "Payments are not set up yet. Please try again later.");
        }

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId)
            ?? throw new ArgumentException("User not found.");

        var baseUrl = !string.IsNullOrWhiteSpace(ConfiguredBackendUrl)
            ? ConfiguredBackendUrl
            : (backendBaseUrl ?? "").TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Payments are not set up yet. Please try again later.");
        }

        // PayU txnid: max 25 chars, har baar naya
        var txnId = $"EX{userId}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        if (txnId.Length > 25)
            txnId = txnId[..25];

        // Hash me aur form me bilkul same string jaani chahiye
        var amount = (plan.AmountPaise / 100m)
            .ToString("0.00", CultureInfo.InvariantCulture);

        var productInfo = $"Examo {plan.Name}";
        var firstName = CleanFirstName(student.Name);
        var email = (student.Email ?? "").Trim();
        var phone = CleanPhone(student.MobileNumber);

        // udf1 me frontend ka URL jaata hai; PayU wapas bhejta hai aur hash me bhi judta hai
        var frontendUrl = ResolveFrontendUrl(frontendOrigin);

        var hash = Sha512Hex(string.Join("|", new[]
        {
            MerchantKey,
            txnId,
            amount,
            productInfo,
            firstName,
            email,
            // udf1 (frontend URL), udf2..udf5 (khaali) + 5 reserved (khaali)
            frontendUrl, "", "", "", "",
            "", "", "", "", "",
            MerchantSalt
        }));

        _db.Payments.Add(new Payment
        {
            UserId = userId,
            PlanKey = plan.Key,
            AmountPaise = plan.AmountPaise,
            Currency = "INR",
            RazorpayOrderId = txnId,
            Status = "Created",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var callback = $"{baseUrl}/api/Subscription/payu/callback";

        return new PayUCheckoutResult
        {
            ActionUrl = ActionUrl,
            TxnId = txnId,
            PlanKey = plan.Key,
            PlanName = plan.Name,
            Fields = new Dictionary<string, string>
            {
                ["key"] = MerchantKey,
                ["txnid"] = txnId,
                ["amount"] = amount,
                ["productinfo"] = productInfo,
                ["firstname"] = firstName,
                ["email"] = email,
                ["phone"] = phone,
                ["udf1"] = frontendUrl,
                ["surl"] = callback,
                ["furl"] = callback,
                ["hash"] = hash
            }
        };
    }

    // =====================================================
    // STEP 2: PayU ka response (success / failure) -> hash verify
    // =====================================================
    public async Task<PayUCallbackResult> HandlePayUResponseAsync(
        IDictionary<string, string> form)
    {
        string Get(string name) =>
            form.TryGetValue(name, out var v) ? (v ?? "").Trim() : "";

        var txnId = Get("txnid");
        var status = Get("status");

        // Hash sahi hone ke baad udf1 se asli frontend URL milta hai
        var frontendBase = DefaultFrontendUrl;

        PayUCallbackResult Done(bool ok, string result) => new()
        {
            Success = ok,
            TxnId = txnId,
            RedirectUrl =
                $"{frontendBase}/?payment={result}&txn={Uri.EscapeDataString(txnId)}"
        };

        if (string.IsNullOrWhiteSpace(txnId) ||
            string.IsNullOrWhiteSpace(Get("hash")) ||
            string.IsNullOrWhiteSpace(MerchantSalt))
        {
            return Done(false, "error");
        }

        // Reverse hash:
        // [additionalCharges|]SALT|status|(5 khaali)|udf5..udf1|email|firstname|productinfo|amount|txnid|key
        var parts = new List<string>();

        var additionalCharges = Get("additionalCharges");

        if (!string.IsNullOrWhiteSpace(additionalCharges))
            parts.Add(additionalCharges);

        parts.Add(MerchantSalt);
        parts.Add(status);
        parts.AddRange(new[] { "", "", "", "", "" });
        parts.Add(Get("udf5"));
        parts.Add(Get("udf4"));
        parts.Add(Get("udf3"));
        parts.Add(Get("udf2"));
        parts.Add(Get("udf1"));
        parts.Add(Get("email"));
        parts.Add(Get("firstname"));
        parts.Add(Get("productinfo"));
        parts.Add(Get("amount"));
        parts.Add(txnId);
        parts.Add(Get("key"));

        var expected = Sha512Hex(string.Join("|", parts));

        if (!SafeEquals(expected, Get("hash")) ||
            !string.Equals(Get("key"), MerchantKey, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "PayU hash mismatch for txn {TxnId}", txnId);

            return Done(false, "error");
        }

        // Hash verify ho chuka hai, ab udf1 par bharosa kar sakte hain
        frontendBase = ResolveFrontendUrl(Get("udf1"));

        var payment = await _db.Payments
            .FirstOrDefaultAsync(x => x.RazorpayOrderId == txnId);

        if (payment == null)
        {
            _logger.LogWarning("PayU response for unknown txn {TxnId}", txnId);

            return Done(false, "error");
        }

        // Amount wahi hona chahiye jo humne maanga tha
        if (!decimal.TryParse(
                Get("amount"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var paidAmount) ||
            (int)Math.Round(paidAmount * 100m) != payment.AmountPaise)
        {
            _logger.LogWarning(
                "PayU amount mismatch for txn {TxnId}", txnId);

            return Done(false, "error");
        }

        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var payuId = Get("mihpayid");

            await MarkPaidAsync(
                payment,
                string.IsNullOrWhiteSpace(payuId) ? txnId : payuId);

            return Done(true, "success");
        }

        // Failed / cancelled: Paid payment ko kabhi neeche nahi laate
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Payments""
               SET ""Status"" = 'Failed'
               WHERE ""Id"" = {payment.Id} AND ""Status"" = 'Created'");

        return Done(false, "failed");
    }

    // =====================================================
    // Payment ko Paid karo, plan chalu karo, email bhejo.
    // Callback aur webhook dono aa jayein to bhi sirf ek baar chalta hai.
    // =====================================================
    private async Task MarkPaidAsync(Payment payment, string payuPaymentId)
    {
        var now = DateTime.UtcNow;

        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Payments""
               SET ""Status"" = 'Paid',
                   ""RazorpayPaymentId"" = {payuPaymentId},
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

        await SendPaymentEmailAsync(payment, plan, payuPaymentId, end);
    }

    private async Task SendPaymentEmailAsync(
        Payment payment,
        SubscriptionPlan plan,
        string payuPaymentId,
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
                $"Payment ID: {payuPaymentId}\n" +
                $"Transaction ID: {payment.RazorpayOrderId}\n\n" +
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

    // ---------------- Helpers ----------------

    // PayU firstname me special characters pasand nahi karta
    private static string CleanFirstName(string? name)
    {
        var first = (name ?? "")
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";

        var cleaned = new string(first.Where(char.IsLetterOrDigit).ToArray());

        if (cleaned.Length > 30)
            cleaned = cleaned[..30];

        return cleaned.Length == 0 ? "Student" : cleaned;
    }

    private static string CleanPhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());

        if (digits.Length >= 10)
            return digits[^10..];

        return "9999999999";
    }

    private static string Sha512Hex(string text)
    {
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(text));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool SafeEquals(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var y = Encoding.UTF8.GetBytes(b.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(x, y);
    }
}