using System.Globalization;
using System.Security.Claims;

using Examo.Filters;
using Examo.Models;
using Examo.Services;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

// Admin > Payments: sab users ka subscription status, edit / renew, plan price.
[ApiController]
[Route("api/Admin/payments")]
[Authorize(Roles = "Admin")]
public class AdminPaymentsController : ControllerBase
{
    private readonly ExamoDbContext _context;
    private readonly ISubscriptionService _subscriptions;
    private readonly IConfiguration _config;

    public AdminPaymentsController(
        ExamoDbContext context,
        ISubscriptionService subscriptions,
        IConfiguration config)
    {
        _context = context;
        _subscriptions = subscriptions;
        _config = config;
    }

    // =====================================================
    // GET /api/Admin/payments/subscriptions?status=&plan=&search=
    // =====================================================
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] string? status = null,
        [FromQuery] string? plan = null,
        [FromQuery] string? search = null)
    {
        var now = DateTime.UtcNow;

        var plans = await _subscriptions.GetPlansAsync();

        var trialDays = await _subscriptions.GetTrialDaysAsync();

        var students = await _context.Students
            .AsNoTracking()
            .Where(x => x.Role != "Admin")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.TrialStartsAt,
                x.TrialEndsAt
            })
            .ToListAsync();

        var subs = await _context.UserSubscriptions
            .AsNoTracking()
            .ToListAsync();

        var subsByUser = subs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var paidByUser = await _context.Payments
            .AsNoTracking()
            .Where(x => x.Status == "Paid")
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Paise = g.Sum(x => (long)x.AmountPaise)
            })
            .ToDictionaryAsync(x => x.UserId, x => x.Paise);

        var rows = students
            .Select(s =>
            {
                subsByUser.TryGetValue(s.Id, out var mine);

                paidByUser.TryGetValue(s.Id, out var paise);

                return BuildRow(
                    s.Id,
                    s.Name,
                    s.Email,
                    s.MobileNumber,
                    s.TrialStartsAt,
                    s.TrialEndsAt,
                    mine ?? new List<UserSubscription>(),
                    paise,
                    plans,
                    trialDays,
                    now);
            })
            .ToList();

        // Cards ke numbers poori list se (filter se nahi badalte)
        var stats = new
        {
            total = rows.Count,
            active = rows.Count(x => x.Status == "Active"),
            trial = rows.Count(x => x.Status == "Trial"),
            expired = rows.Count(x => x.Status == "Expired"),
            revenue = paidByUser.Values.Sum() / 100m
        };

        var text = (search ?? "").Trim().ToLowerInvariant();

        var filtered = rows
            .Where(x =>
                (string.IsNullOrWhiteSpace(status) || x.Status == status) &&
                (string.IsNullOrWhiteSpace(plan) ||
                    string.Equals(x.PlanKey, plan, StringComparison.OrdinalIgnoreCase)) &&
                (text.Length == 0 ||
                    x.Name.ToLowerInvariant().Contains(text) ||
                    x.Email.ToLowerInvariant().Contains(text) ||
                    (x.MobileNumber ?? "").Contains(text)))
            .OrderBy(x => StatusRank(x.Status))
            .ThenBy(x => x.Status == "Expired"
                ? -(x.EndsAt?.Ticks ?? 0)
                : (x.EndsAt?.Ticks ?? 0))
            .ThenBy(x => x.Name)
            .ToList();

        return Ok(new
        {
            items = filtered,
            stats
        });
    }

    // =====================================================
    // PUT /api/Admin/payments/subscriptions/{userId}
    // Plan / start / end date badlo. { planKey, startsAt, endsAt, note }
    // =====================================================
    public class EditSubscriptionRequest
    {
        public string PlanKey { get; set; } = string.Empty;

        // yyyy-MM-dd
        public string StartsAt { get; set; } = string.Empty;

        // yyyy-MM-dd
        public string EndsAt { get; set; } = string.Empty;

        public string? Note { get; set; }
    }

    [HttpPut("subscriptions/{userId:int}")]
    public async Task<IActionResult> EditSubscription(
        int userId,
        [FromBody] EditSubscriptionRequest request)
    {
        var plan = await FindPlanAsync(request.PlanKey);

        if (plan == null)
            return BadRequest(new { message = "Please choose a valid plan." });

        if (!TryParseDate(request.StartsAt, out var startDate) ||
            !TryParseDate(request.EndsAt, out var endDate))
        {
            return BadRequest(new
            {
                message = "Please select both the start and end dates."
            });
        }

        if (endDate < startDate)
        {
            return BadRequest(new
            {
                message = "The end date cannot be before the start date."
            });
        }

        if ((endDate.DayNumber - startDate.DayNumber) > 3650)
        {
            return BadRequest(new
            {
                message = "A plan cannot be longer than 10 years."
            });
        }

        var student = await FindStudentAsync(userId);

        if (student == null)
            return NotFound(new { message = "User not found." });

        if (student.Role == "Admin")
        {
            return BadRequest(new
            {
                message = "Admins do not need a plan."
            });
        }

        var startsAt = ToUtc(startDate, endOfDay: false);
        var endsAt = ToUtc(endDate, endOfDay: true);

        var now = DateTime.UtcNow;

        var mine = await _context.UserSubscriptions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var current = mine
            .Where(x => x.EndsAt > now)
            .OrderByDescending(x => x.EndsAt)
            .FirstOrDefault();

        if (current != null)
        {
            // Baaki chalu / aage ke rows hata do, warna edit ka asar nahi dikhega
            var others = mine
                .Where(x => x.EndsAt > now && x.Id != current.Id)
                .ToList();

            if (others.Count > 0)
                _context.UserSubscriptions.RemoveRange(others);

            current.PlanKey = plan.Key;
            current.StartsAt = startsAt;
            current.EndsAt = endsAt;
        }
        else
        {
            _context.UserSubscriptions.Add(new UserSubscription
            {
                UserId = userId,
                PlanKey = plan.Key,
                StartsAt = startsAt,
                EndsAt = endsAt,
                PaymentId = null,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();

        Audit(
            "Edit Subscription",
            $"{student.Name} ({student.Email}): {plan.Name}, " +
            $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}" +
            NoteText(request.Note));

        return Ok(new { message = "Subscription updated." });
    }

    // =====================================================
    // POST /api/Admin/payments/subscriptions/{userId}/renew
    // { planKey, days, note }
    // =====================================================
    public class RenewSubscriptionRequest
    {
        public string PlanKey { get; set; } = string.Empty;

        public int Days { get; set; }

        public string? Note { get; set; }
    }

    [HttpPost("subscriptions/{userId:int}/renew")]
    public async Task<IActionResult> RenewSubscription(
        int userId,
        [FromBody] RenewSubscriptionRequest request)
    {
        var plan = await FindPlanAsync(request.PlanKey);

        if (plan == null)
            return BadRequest(new { message = "Please choose a valid plan." });

        if (request.Days < 1 || request.Days > 3650)
        {
            return BadRequest(new
            {
                message = "Days must be a whole number between 1 and 3650."
            });
        }

        var student = await FindStudentAsync(userId);

        if (student == null)
            return NotFound(new { message = "User not found." });

        if (student.Role == "Admin")
        {
            return BadRequest(new
            {
                message = "Admins do not need a plan."
            });
        }

        var now = DateTime.UtcNow;

        // Chalu plan ho to uske end se aage, warna aaj se
        var runningEnd = await _context.UserSubscriptions
            .Where(x => x.UserId == userId && x.EndsAt > now)
            .Select(x => (DateTime?)x.EndsAt)
            .MaxAsync();

        var startsAt = runningEnd ?? now;
        var endsAt = startsAt.AddDays(request.Days);

        _context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanKey = plan.Key,
            StartsAt = startsAt,
            EndsAt = endsAt,
            PaymentId = null,
            CreatedAt = now
        });

        await _context.SaveChangesAsync();

        Audit(
            "Renew Subscription",
            $"{student.Name} ({student.Email}): {plan.Name} +{request.Days} days" +
            NoteText(request.Note));

        return Ok(new
        {
            message = "Subscription renewed.",
            endsAt
        });
    }

    // =====================================================
    // GET /api/Admin/payments/plans  ->  [{ key, name, amount, days }]
    // =====================================================
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptions.GetPlansAsync();

        return Ok(plans.Select(p => new
        {
            key = p.Key,
            name = p.Name,
            amount = p.AmountPaise / 100m,
            days = p.Days
        }));
    }

    // =====================================================
    // PUT /api/Admin/payments/plans/{key}   { amount }   (rupees)
    // =====================================================
    public class UpdatePlanPriceRequest
    {
        public decimal Amount { get; set; }
    }

    [HttpPut("plans/{key}")]
    public async Task<IActionResult> UpdatePlanPrice(
        string key,
        [FromBody] UpdatePlanPriceRequest request)
    {
        var plan = await FindPlanAsync(key);

        if (plan == null)
            return BadRequest(new { message = "Please choose a valid plan." });

        if (request.Amount < 1 ||
            request.Amount > 100000 ||
            Math.Round(request.Amount, 2) != request.Amount)
        {
            return BadRequest(new
            {
                message =
                    "Price must be between ₹1 and ₹1,00,000 (up to 2 decimals)."
            });
        }

        var oldAmount = plan.AmountPaise / 100m;

        var paise = (int)Math.Round(request.Amount * 100m);

        var admin =
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Name);

        try
        {
            await _subscriptions.SetPlanPriceAsync(plan.Key, paise, admin);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        Audit(
            "Update Plan Price",
            $"{plan.Name}: Rs {oldAmount.ToString("0.##", CultureInfo.InvariantCulture)} " +
            $"to Rs {request.Amount.ToString("0.##", CultureInfo.InvariantCulture)}");

        return Ok(new { message = "Price updated." });
    }

    // =====================================================
    // GET /api/Admin/payments/trial       ->  { days }
    // PUT /api/Admin/payments/trial       { days }   (0 = free trial band)
    // =====================================================
    [HttpGet("trial")]
    public async Task<IActionResult> GetTrial()
    {
        return Ok(new { days = await _subscriptions.GetTrialDaysAsync() });
    }

    public class UpdateTrialRequest
    {
        public int Days { get; set; }
    }

    [HttpPut("trial")]
    public async Task<IActionResult> UpdateTrial(
        [FromBody] UpdateTrialRequest request)
    {
        if (request.Days < 0 || request.Days > 90)
        {
            return BadRequest(new
            {
                message = "Trial must be between 0 and 90 days."
            });
        }

        var oldDays = await _subscriptions.GetTrialDaysAsync();

        var admin =
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Name);

        await _subscriptions.SetTrialDaysAsync(request.Days, admin);

        Audit(
            "Update Free Trial",
            $"Trial: {oldDays} days to {request.Days} days");

        return Ok(new { message = "Trial updated." });
    }

    // ---------------- Helpers ----------------

    private async Task<SubscriptionPlan?> FindPlanAsync(string? key)
    {
        var plans = await _subscriptions.GetPlansAsync();

        return plans.FirstOrDefault(p =>
            string.Equals(p.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private Task<Student?> FindStudentAsync(int userId) =>
        _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

    // Logs & Security me naam + details
    private void Audit(string action, string details)
    {
        HttpContext.Items[AuditLogFilter.ActionKey] = action;
        HttpContext.Items[AuditLogFilter.DetailsKey] = details;
    }

    private static string NoteText(string? note) =>
        string.IsNullOrWhiteSpace(note) ? "" : $" | Note: {note.Trim()}";

    private static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(
            (text ?? "").Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    // Admin ki date India ke time (IST) me maani jaati hai
    private DateTime ToUtc(DateOnly date, bool endOfDay)
    {
        var offset = _config.GetValue("Notifications:UtcOffsetMinutes", 330);

        var local = date.ToDateTime(
            endOfDay ? new TimeOnly(23, 59, 59) : TimeOnly.MinValue);

        return DateTime.SpecifyKind(
            local.AddMinutes(-offset),
            DateTimeKind.Utc);
    }

    private static int StatusRank(string status) =>
        status switch
        {
            "Active" => 0,
            "Trial" => 1,
            "Expired" => 2,
            _ => 3
        };

    private static int DaysUntil(DateTime end, DateTime now) =>
        Math.Max(0, (int)Math.Ceiling((end - now).TotalDays));

    public class SubscriptionRow
    {
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? MobileNumber { get; set; }

        // Active | Trial | Expired | Not Started
        public string Status { get; set; } = "Not Started";

        public string? PlanKey { get; set; }

        public string? PlanName { get; set; }

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public int DaysLeft { get; set; }

        public decimal TotalPaid { get; set; }
    }

    // Ek student ka status (SubscriptionService.GetStatusAsync jaise hi rules)
    private static SubscriptionRow BuildRow(
        int id,
        string name,
        string email,
        string? mobile,
        DateTime? trialStartsAt,
        DateTime? trialEndsAt,
        List<UserSubscription> subs,
        long paidPaise,
        IReadOnlyList<SubscriptionPlan> plans,
        int trialDays,
        DateTime now)
    {
        string? NameOf(string? key) =>
            plans.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Name ?? key;

        var row = new SubscriptionRow
        {
            UserId = id,
            Name = name,
            Email = email,
            MobileNumber = mobile,
            TotalPaid = paidPaise / 100m
        };

        var running = subs
            .Where(x => x.EndsAt > now)
            .OrderByDescending(x => x.EndsAt)
            .ToList();

        if (running.Count > 0)
        {
            var last = running[0];

            row.Status = "Active";
            row.PlanKey = last.PlanKey;
            row.PlanName = NameOf(last.PlanKey);
            row.StartsAt = running.Min(x => x.StartsAt);
            row.EndsAt = last.EndsAt;
            row.DaysLeft = DaysUntil(last.EndsAt, now);

            return row;
        }

        if (trialEndsAt != null && trialEndsAt > now)
        {
            row.Status = "Trial";
            row.PlanName = "Free Trial";
            row.StartsAt = trialStartsAt ?? trialEndsAt.Value.AddDays(-trialDays);
            row.EndsAt = trialEndsAt;
            row.DaysLeft = DaysUntil(trialEndsAt.Value, now);

            return row;
        }

        if (subs.Count > 0)
        {
            var last = subs.OrderByDescending(x => x.EndsAt).First();

            row.Status = "Expired";
            row.PlanKey = last.PlanKey;
            row.PlanName = NameOf(last.PlanKey);
            row.StartsAt = last.StartsAt;
            row.EndsAt = last.EndsAt;

            return row;
        }

        if (trialEndsAt != null)
        {
            row.Status = "Expired";
            row.PlanName = "Free Trial";
            row.StartsAt = trialStartsAt ?? trialEndsAt.Value.AddDays(-trialDays);
            row.EndsAt = trialEndsAt;

            return row;
        }

        // Is user ne abhi Subscription page / status check kiya hi nahi
        row.Status = "Not Started";

        return row;
    }
}