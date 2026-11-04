using System.Text.Json;

using Examo.Filters;
using Examo.Models;
using Examo.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

// Admin > Backup: Content Manager / NotifyMe / Help & Support ka delete hua data.
//   GET     /api/Admin/backups?section=content|notifyme|help&search=abc
//   GET     /api/Admin/backups/5            -> poora saved data
//   POST    /api/Admin/backups/5/restore    -> wapas us section me daal do
//   DELETE  /api/Admin/backups/5            -> hamesha ke liye hata do
// Sirf Admin.
[ApiController]
[Route("api/Admin/backups")]
[Authorize(Roles = "Admin")]
public class AdminBackupsController : ControllerBase
{
    private readonly ExamoDbContext _context;
    private readonly ILogger<AdminBackupsController> _logger;

    public AdminBackupsController(
        ExamoDbContext context,
        ILogger<AdminBackupsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? section = null,
        [FromQuery] string? search = null)
    {
        var query = _context.AdminBackupItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(section))
        {
            var s = section.Trim().ToLowerInvariant();

            query = query.Where(x => x.Section == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";

            query = query.Where(x =>
                EF.Functions.ILike(x.Title, term) ||
                EF.Functions.ILike(x.Summary, term) ||
                EF.Functions.ILike(x.ItemType, term) ||
                (x.DeletedBy != null && EF.Functions.ILike(x.DeletedBy, term)));
        }

        var items = await query
            .OrderByDescending(x => x.DeletedAt)
            .ThenByDescending(x => x.Id)
            .Take(300)
            .Select(x => new
            {
                x.Id,
                x.Section,
                x.ItemType,
                x.OriginalId,
                x.Title,
                x.Summary,
                x.DeletedBy,
                x.DeletedAt
            })
            .ToListAsync();

        // Cards ke numbers poori table se (filter se nahi badalte)
        var stats = await _context.AdminBackupItems
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(g => new
            {
                total = g.Count(),
                content = g.Count(x => x.Section == AdminBackupWriter.SectionContent),
                notifyme = g.Count(x => x.Section == AdminBackupWriter.SectionNotifyMe),
                help = g.Count(x => x.Section == AdminBackupWriter.SectionHelp)
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            stats = stats ?? new { total = 0, content = 0, notifyme = 0, help = 0 },
            items
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var item = await _context.AdminBackupItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Backup item not found." });

        JsonElement data;

        try
        {
            using var doc = JsonDocument.Parse(item.DataJson);

            data = doc.RootElement.Clone();
        }
        catch
        {
            return BadRequest(new { message = "Saved data is not readable." });
        }

        return Ok(new
        {
            item.Id,
            item.Section,
            item.ItemType,
            item.OriginalId,
            item.Title,
            item.Summary,
            item.DeletedBy,
            item.DeletedAt,
            data
        });
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var backup = await _context.AdminBackupItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (backup == null)
            return NotFound(new { message = "Backup item not found." });

        string where;

        try
        {
            switch (backup.Section)
            {
                case AdminBackupWriter.SectionContent:
                    {
                        var job = JsonSerializer.Deserialize<JobPost>(
                            backup.DataJson, AdminBackupWriter.JsonOptions);

                        if (job == null)
                            return BadRequest(new { message = "Saved data is not readable." });

                        // Naya Id milega (purani Id ab free ho chuki hai)
                        job.Id = 0;

                        _context.JobPosts.Add(job);

                        where = "Content Manager";
                        break;
                    }

                case AdminBackupWriter.SectionHelp:
                    {
                        var msg = JsonSerializer.Deserialize<SupportMessage>(
                            backup.DataJson, AdminBackupWriter.JsonOptions);

                        if (msg == null)
                            return BadRequest(new { message = "Saved data is not readable." });

                        msg.Id = 0;

                        // Restore karne par "New" badge dobara nahi aana chahiye
                        msg.AdminSeen = true;

                        _context.SupportMessages.Add(msg);

                        where = "Help & Support";
                        break;
                    }

                case AdminBackupWriter.SectionNotifyMe:
                    {
                        var note = JsonSerializer.Deserialize<AdminNotification>(
                            backup.DataJson, AdminBackupWriter.JsonOptions);

                        if (note == null)
                            return BadRequest(new { message = "Saved data is not readable." });

                        note.Id = 0;

                        // DedupeKey unique hai, isliye naya key
                        note.DedupeKey = $"restored:{Guid.NewGuid():N}";

                        _context.AdminNotifications.Add(note);

                        where = "NotifyMe";
                        break;
                    }

                default:
                    return BadRequest(new { message = "Unknown backup section." });
            }

            _context.AdminBackupItems.Remove(backup);

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup restore failed for item {Id}.", id);

            return StatusCode(500, new { message = "Could not restore this item." });
        }

        HttpContext.Items[AuditLogFilter.ActionKey] = "Restore from Backup";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"{backup.ItemType}: {backup.Title}";

        return Ok(new { message = $"Restored to {where}." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteForever(int id)
    {
        var backup = await _context.AdminBackupItems
            .FirstOrDefaultAsync(x => x.Id == id);

        if (backup == null)
            return NotFound(new { message = "Backup item not found." });

        _context.AdminBackupItems.Remove(backup);

        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] = "Delete Backup Permanently";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"{backup.ItemType}: {backup.Title}";

        return Ok(new { message = "Deleted permanently." });
    }
}
