using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Examo.Models;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ExamoDbContext _context;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionService service,
        ICurrentUserService currentUser,
        ExamoDbContext context,
        ILogger<SubscriptionController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _context = context;
        _logger = logger;
    }

    // GET /api/Subscription/me  -> status + plans + purani payments
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        var userId = _currentUser.UserId;

        var status = await _service.GetStatusAsync(userId);

        var plans = await _service.GetPlansAsync();

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "Paid")
            .OrderByDescending(x => x.PaidAt)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.PlanKey,
                Amount = x.AmountPaise / 100m,
                x.Currency,
                PaymentId = x.RazorpayPaymentId,
                TxnId = x.RazorpayOrderId,
                x.PaidAt,

                // Is payment se jo plan mila uski start / end date (slip ke liye)
                StartsAt = _context.UserSubscriptions
                    .Where(s => s.PaymentId == x.Id)
                    .Select(s => (DateTime?)s.StartsAt)
                    .FirstOrDefault(),
                EndsAt = _context.UserSubscriptions
                    .Where(s => s.PaymentId == x.Id)
                    .Select(s => (DateTime?)s.EndsAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new
        {
            status.Status,
            status.HasAccess,
            status.TrialEndsAt,
            status.PlanKey,
            status.PlanName,
            status.EndsAt,
            status.DaysLeft,
            trialDays = await _service.GetTrialDaysAsync(),
            plans = plans.Select(p => new
            {
                p.Key,
                p.Name,
                Amount = p.AmountPaise / 100m,
                p.Days
            }),
            payments
        });
    }

    public class CheckoutRequest
    {
        public string Plan { get; set; } = string.Empty;
    }

    // POST /api/Subscription/checkout   { "plan": "monthly" | "yearly" }
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request)
    {
        try
        {
            // PayU yahin wapas aayega (agar PayU__BackendUrl set nahi hai to)
            var scheme = Request.Headers["X-Forwarded-Proto"]
                .FirstOrDefault()?
                .Split(',')[0]
                .Trim();

            if (string.IsNullOrWhiteSpace(scheme))
                scheme = Request.Scheme;

            var baseUrl = $"{scheme}://{Request.Host}";

            // User kis website se aaya (browser khud Origin bhejta hai)
            var origin = Request.Headers["Origin"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(origin) &&
                Uri.TryCreate(
                    Request.Headers["Referer"].FirstOrDefault(),
                    UriKind.Absolute,
                    out var referer))
            {
                origin = $"{referer.Scheme}://{referer.Authority}";
            }

            var result = await _service.CreateCheckoutAsync(
                _currentUser.UserId,
                request.Plan,
                baseUrl,
                origin);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
    }

    // POST /api/Subscription/payu/callback
    // PayU user ke browser ko yahan POST karke bhejta hai (surl / furl).
    // Login nahi hota, isliye security PayU ke hash se hoti hai.
    [HttpPost("payu/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> PayUCallback()
    {
        var form = await ReadFieldsAsync();

        try
        {
            var result = await _service.HandlePayUResponseAsync(form);

            return Redirect(result.RedirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayU callback could not be processed.");

            return StatusCode(500);
        }
    }

    // POST /api/Subscription/payu/webhook   (optional)
    // PayU dashboard me webhook lagao to tab band hone par bhi payment confirm hoti hai.
    [HttpPost("payu/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayUWebhook()
    {
        var form = await ReadFieldsAsync();

        try
        {
            await _service.HandlePayUResponseAsync(form);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayU webhook could not be processed.");

            return StatusCode(500);
        }
    }

    // Form ya JSON dono se fields padho
    private async Task<Dictionary<string, string>> ReadFieldsAsync()
    {
        var fields = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();

            foreach (var item in form)
                fields[item.Key] = item.Value.ToString();

            return fields;
        }

        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                Request.Body);

            if (doc.RootElement.ValueKind ==
                System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    fields[prop.Name] =
                        prop.Value.ValueKind ==
                        System.Text.Json.JsonValueKind.String
                            ? prop.Value.GetString() ?? ""
                            : prop.Value.ToString();
                }
            }
        }
        catch
        {
            // Khaali / galat body: khaali fields wapas, hash check fail ho jayega
        }

        return fields;
    }
}