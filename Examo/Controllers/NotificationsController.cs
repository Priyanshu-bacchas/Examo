using System.Security.Cryptography;
using System.Text;

using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly IConfiguration _config;

    public NotificationsController(
        INotificationService service,
        IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    // GET /api/Notifications?unreadOnly=false&take=100
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 100)
    {
        var items = await _service.GetMineAsync(unreadOnly, take);
        var unread = await _service.GetUnreadCountAsync();

        return Ok(new
        {
            unreadCount = unread,
            items = items.Select(x => new
            {
                x.Id,
                x.Type,
                x.Title,
                x.Message,
                x.ExamName,
                x.IsRead,
                x.CreatedAt
            })
        });
    }

    // GET /api/Notifications/unread-count  (topbar bell badge)
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        return Ok(new
        {
            unreadCount = await _service.GetUnreadCountAsync()
        });
    }

    // PUT /api/Notifications/5/read
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var ok = await _service.MarkReadAsync(id);

        if (!ok)
            return NotFound(new { message = "Notification not found." });

        return NoContent();
    }

    // PUT /api/Notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var count = await _service.MarkAllReadAsync();

        return Ok(new { updated = count });
    }

    // DELETE /api/Notifications/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _service.DeleteAsync(id);

        if (!ok)
            return NotFound(new { message = "Notification not found." });

        return NoContent();
    }

    // DELETE /api/Notifications  (saari clear)
    [HttpDelete]
    public async Task<IActionResult> ClearAll()
    {
        var count = await _service.ClearAllAsync();

        return Ok(new { deleted = count });
    }

    // =====================================================
    // CRON: external scheduler (cron-job.org / UptimeRobot) ke liye
    //   GET /api/Notifications/cron?key=YOUR_SECRET
    // Render par env var:  Notifications__CronKey = YOUR_SECRET
    // Key set nahi hai to ye endpoint 404 deta hai (band rehta hai).
    // =====================================================
    [AllowAnonymous]
    [HttpGet("cron")]
    [HttpPost("cron")]
    public async Task<IActionResult> Cron([FromQuery] string? key)
    {
        var expected =
            _config["Notifications:CronKey"]
            ?? Environment.GetEnvironmentVariable("NOTIFICATIONS_CRON_KEY");

        if (string.IsNullOrWhiteSpace(expected))
            return NotFound();

        var given = key ?? string.Empty;

        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(given),
            Encoding.UTF8.GetBytes(expected));

        if (!valid)
            return Unauthorized(new { message = "Invalid key." });

        var summary = await _service.RunDueRemindersAsync(
            HttpContext.RequestAborted);

        return Ok(new
        {
            summary.Created,
            summary.EmailsSent,
            summary.Skipped,
            ranAt = DateTime.UtcNow
        });
    }
}