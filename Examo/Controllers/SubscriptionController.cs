using System.Text;

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
                x.RazorpayPaymentId,
                x.RazorpayOrderId,
                x.PaidAt
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
            plans = _service.Plans.Select(p => new
            {
                p.Key,
                p.Name,
                Amount = p.AmountPaise / 100m,
                p.Days
            }),
            payments
        });
    }

    public class CreateOrderRequest
    {
        public string Plan { get; set; } = string.Empty;
    }

    // POST /api/Subscription/order   { "plan": "monthly" | "yearly" }
    [HttpPost("order")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request)
    {
        try
        {
            var result = await _service.CreateOrderAsync(
                _currentUser.UserId,
                request.Plan);

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

    public class VerifyRequest
    {
        public string OrderId { get; set; } = string.Empty;

        public string PaymentId { get; set; } = string.Empty;

        public string Signature { get; set; } = string.Empty;
    }

    // POST /api/Subscription/verify
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
    {
        try
        {
            var status = await _service.VerifyPaymentAsync(
                _currentUser.UserId,
                request.OrderId,
                request.PaymentId,
                request.Signature);

            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/Subscription/webhook  (Razorpay khud bulata hai, login nahi hota)
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        string body;

        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        var signature = Request.Headers["X-Razorpay-Signature"].ToString();

        try
        {
            await _service.HandleWebhookAsync(body, signature);

            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Webhook could not be processed.");

            return StatusCode(500);
        }
    }
}
