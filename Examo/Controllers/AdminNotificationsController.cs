using Examo.Models;
using Examo.Services;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

// Admin > NotifyMe: students ke Help messages aur subscriptions ki notifications.
// Sirf Admin dekh sakta hai.
[ApiController]
[Route("api/Admin/notifications")]
[Authorize(Roles = "Admin")]
public class AdminNotificationsController : ControllerBase
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AdminNotificationsController(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Delete se pehle Backup me copy (Admin > Backup se restore ho sakta hai)
    private static void StageBackup(
        ExamoDbContext context,
        AdminNotification item,
        string? deletedBy)
    {
        AdminBackupWriter.Stage(
            context,
            AdminBackupWriter.SectionNotifyMe,
            item.Type == AdminNotificationType.Support
                ? "Help notification"
                : "Subscription notification",
            item.Id,
            item.Title,
            item.Message,
            item,
            deletedBy);
    }

    // GET /api/Admin/notifications?type=support|subscription&unreadOnly=true&search=abc
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type = null,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? search = null)
    {
        var query = _context.AdminNotifications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLowerInvariant();

            query = query.Where(x => x.Type == t);
        }

        if (unreadOnly)
            query = query.Where(x => !x.IsRead);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";

            query = query.Where(x =>
                EF.Functions.ILike(x.Title, term) ||
                EF.Functions.ILike(x.Message, term) ||
                (x.UserName != null && EF.Functions.ILike(x.UserName, term)) ||
                (x.UserEmail != null && EF.Functions.ILike(x.UserEmail, term)));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(300)
            .Select(x => new
            {
                x.Id,
                x.Type,
                x.Title,
                x.Message,
                x.UserId,
                x.UserName,
                x.UserEmail,
                x.RefId,
                x.IsRead,
                x.CreatedAt
            })
            .ToListAsync();

        // Cards ke numbers poori table se (filter se nahi badalte)
        var stats = await _context.AdminNotifications
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(g => new
            {
                total = g.Count(),
                unread = g.Count(x => !x.IsRead),
                support = g.Count(x => x.Type == AdminNotificationType.Support),
                subscription = g.Count(x => x.Type == AdminNotificationType.Subscription)
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            stats = stats ?? new { total = 0, unread = 0, support = 0, subscription = 0 },
            items
        });
    }

    // GET /api/Admin/notifications/unread-count  (sidebar badge)
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await _context.AdminNotifications
            .AsNoTracking()
            .CountAsync(x => !x.IsRead);

        return Ok(new { unreadCount = count });
    }

    // PUT /api/Admin/notifications/5/read
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var item = await _context.AdminNotifications
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Notification not found." });

        if (!item.IsRead)
        {
            item.IsRead = true;

            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Marked as read." });
    }

    // PUT /api/Admin/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _context.AdminNotifications
            .Where(x => !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));

        return Ok(new { message = "All notifications marked as read." });
    }

    // DELETE /api/Admin/notifications/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.AdminNotifications
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Notification not found." });

        StageBackup(
            _context,
            item,
            await AdminBackupWriter.GetAdminNameAsync(
                _context, _currentUser.UserIdOrNull));

        _context.AdminNotifications.Remove(item);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Notification deleted." });
    }

    // DELETE /api/Admin/notifications/clear-read  (sirf padhi hui hatao)
    [HttpDelete("clear-read")]
    public async Task<IActionResult> ClearRead()
    {
        var readItems = await _context.AdminNotifications
            .Where(x => x.IsRead)
            .ToListAsync();

        var adminName = await AdminBackupWriter.GetAdminNameAsync(
            _context, _currentUser.UserIdOrNull);

        foreach (var item in readItems)
            StageBackup(_context, item, adminName);

        _context.AdminNotifications.RemoveRange(readItems);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"{readItems.Count} notification(s) cleared."
        });
    }
}

// Type ke naam ek jagah (EF query me constant chahiye)
internal static class AdminNotificationType
{
    public const string Support = "support";
    public const string Subscription = "subscription";
}
