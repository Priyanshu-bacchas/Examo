using Examo.DTOs.Support;
using Examo.Filters;
using Examo.Models;
using Examo.Services;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Examo.Controllers;

// Help section: FAQ list, search aur Contact Us (login zaroori, global AuthorizeFilter se)
// Admin ke liye: aaye hue support messages dekhna / resolve / delete
[ApiController]
[Route("api/[controller]")]
public class HelpController : ControllerBase
{
    // Spam rokne ke liye: ek user 10 minute me max 3 messages bhej sakta hai
    private const int MaxMessages = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private const string StatusOpen = "Open";
    private const string StatusResolved = "Resolved";

    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;

    public HelpController(
        ExamoDbContext context,
        ICurrentUserService currentUser,
        IMemoryCache cache,
        IEmailService emailService)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
        _emailService = emailService;
    }

    // =====================================================
    // STUDENT SIDE
    // =====================================================

    // GET /api/Help/content  ->  featured cards + saare FAQ
    [HttpGet("content")]
    public IActionResult GetContent()
    {
        return Ok(HelpContent.GetContent());
    }

    // GET /api/Help/search?q=forgot password  ->  milte-julte jawab
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string? q)
    {
        var text = (q ?? string.Empty).Trim();

        if (text.Length > 200)
            text = text[..200];

        var results = HelpContent.Search(text);

        return Ok(new FaqSearchResultDto
        {
            Query = text,
            Found = results.Count > 0,
            Results = results,
            Message = results.Count > 0
                ? string.Empty
                : "We could not find an exact answer. Please try different words."
        });
    }

    // POST /api/Help/contact  ->  Contact Us form (database me save hota hai)
    [HttpPost("contact")]
    public async Task<IActionResult> Contact(
        [FromBody] ContactSupportDto dto)
    {
        var subject = dto.Subject.Trim();
        var message = dto.Message.Trim();

        if (subject.Length == 0 || message.Length < 10)
        {
            return BadRequest(new
            {
                message = "Please fill in all the fields."
            });
        }

        // Name / Email client se nahi liye jaate: login user ke account (DB) se aate hain
        var userId = _currentUser.UserIdOrNull;

        if (userId == null)
            return Unauthorized(new { message = "Please log in again." });

        var account = await _context.Students
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new { x.Name, x.Email })
            .FirstOrDefaultAsync();

        if (account == null)
            return Unauthorized(new { message = "Account not found. Please log in again." });

        var name = account.Name;
        var email = account.Email;

        // Rate limit (per user)
        var limitKey = $"help-contact:{userId.Value}";

        var count = _cache.TryGetValue(limitKey, out int existing)
            ? existing
            : 0;

        if (count >= MaxMessages)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message =
                    "You have sent too many messages. Please try again after a few minutes."
            });
        }

        _context.SupportMessages.Add(new SupportMessage
        {
            UserId = userId,
            Name = name,
            Email = email,
            Subject = subject,
            Message = message,
            Status = StatusOpen,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _cache.Set(limitKey, count + 1, Window);

        HttpContext.Items[AuditLogFilter.ActionKey] = "Contact Support";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"Support message sent. Subject: {subject}";

        return Ok(new ContactSupportResponseDto
        {
            Message = "Your message has been submitted successfully."
        });
    }

    // =====================================================
    // ADMIN SIDE
    // =====================================================

    // GET /api/Help/messages?status=Open&search=login
    [HttpGet("messages")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? status,
        [FromQuery] string? search)
    {
        var query = _context.SupportMessages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = query.Where(x => x.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";

            query = query.Where(x =>
                EF.Functions.ILike(x.Name, term) ||
                EF.Functions.ILike(x.Email, term) ||
                EF.Functions.ILike(x.Subject, term) ||
                EF.Functions.ILike(x.Message, term));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .Select(x => new SupportMessageDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Name = x.Name,
                Email = x.Email,
                Subject = x.Subject,
                Message = x.Message,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                ResolvedAt = x.ResolvedAt,
                AdminReply = x.AdminReply,
                RepliedAt = x.RepliedAt,
                RepliedBy = x.RepliedBy
            })
            .ToListAsync();

        // Top cards ke liye totals (filter se independent)
        var last7Days = DateTime.UtcNow.AddDays(-7);

        var stats = await _context.SupportMessages
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(g => new SupportMessageStatsDto
            {
                Total = g.Count(),
                Open = g.Count(x => x.Status == StatusOpen),
                Resolved = g.Count(x => x.Status == StatusResolved),
                Last7Days = g.Count(x => x.CreatedAt >= last7Days)
            })
            .FirstOrDefaultAsync() ?? new SupportMessageStatsDto();

        return Ok(new SupportMessageListDto
        {
            Stats = stats,
            Items = items
        });
    }

    // PUT /api/Help/messages/5/status   body: { "status": "Resolved" }
    [HttpPut("messages/{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateSupportStatusDto dto)
    {
        var status = (dto.Status ?? string.Empty).Trim();

        if (!status.Equals(StatusOpen, StringComparison.OrdinalIgnoreCase) &&
            !status.Equals(StatusResolved, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Status must be Open or Resolved."
            });
        }

        var item = await _context.SupportMessages
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Message not found." });

        var resolved = status.Equals(
            StatusResolved,
            StringComparison.OrdinalIgnoreCase);

        item.Status = resolved ? StatusResolved : StatusOpen;
        item.ResolvedAt = resolved ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] =
            "Update Support Message";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"Support message #{id} marked {item.Status}.";

        return Ok(new { message = $"Marked as {item.Status}." });
    }

    // POST /api/Help/messages/5/reply   body: { "reply": "...", "markResolved": true }
    // Reply student ke email par Resend se jaata hai aur DB me save hota hai
    [HttpPost("messages/{id:int}/reply")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReplyToMessage(
        int id,
        [FromBody] ReplySupportDto dto)
    {
        var reply = (dto.Reply ?? string.Empty).Trim();

        if (reply.Length < 2)
        {
            return BadRequest(new
            {
                message = "Please write a reply."
            });
        }

        var item = await _context.SupportMessages
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Message not found." });

        // Reply karne wale admin ka naam
        var adminName = "Examo Support";
        var adminId = _currentUser.UserIdOrNull;

        if (adminId != null)
        {
            var foundName = await _context.Students
                .AsNoTracking()
                .Where(x => x.Id == adminId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(foundName))
                adminName = foundName;
        }

        // Pehle email bhejo; fail ho to DB me kuch save nahi hoga
        try
        {
            await _emailService.SendSupportReplyAsync(
                item.Email,
                item.Name,
                item.Subject,
                item.Message,
                reply);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = ex.Message
            });
        }

        item.AdminReply = reply;
        item.RepliedAt = DateTime.UtcNow;
        item.RepliedBy = adminName;

        if (dto.MarkResolved)
        {
            item.Status = StatusResolved;
            item.ResolvedAt ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] =
            "Reply Support Message";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"Reply sent for support message #{id}.";

        return Ok(new
        {
            message = $"Reply sent to {item.Email}."
        });
    }

    // DELETE /api/Help/messages/5
    [HttpDelete("messages/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var item = await _context.SupportMessages
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Message not found." });

        _context.SupportMessages.Remove(item);

        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] =
            "Delete Support Message";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"Support message #{id} deleted.";

        return Ok(new { message = "Message deleted." });
    }
}