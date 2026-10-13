using Examo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Examo.Controllers;

// Analytics (User Lifecycle, growth) - sirf Admin
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private const string CacheKey = "admin-analytics-users";

    private readonly ExamoDbContext _db;
    private readonly IMemoryCache _cache;

    public AnalyticsController(
        ExamoDbContext db,
        IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // Har signup ki date + "retained" (signup ke baad kisi doosre din dobara login kiya).
    // Charts, cohorts aur growth frontend me (local time ke hisaab se) banaye jaate hain.
    [HttpGet("users")]
    public async Task<IActionResult> Users(
        [FromQuery] bool fresh = false)
    {
        if (!fresh &&
            _cache.TryGetValue(CacheKey, out object? cached) &&
            cached != null)
        {
            return Ok(cached);
        }

        var since = DateTime.UtcNow.AddDays(-190);

        var totalUsers = await _db.Students.CountAsync();

        var recent = await _db.Students
            .AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CreatedAt })
            .ToListAsync();

        var ids = recent.Select(x => x.Id).ToList();

        var lastLogins = new Dictionary<int, DateTime>();

        if (ids.Count > 0)
        {
            lastLogins = await _db.ActivityLogs
                .AsNoTracking()
                .Where(x =>
                    x.Action == "Login" &&
                    x.Success &&
                    x.UserId != null &&
                    ids.Contains(x.UserId.Value))
                .GroupBy(x => x.UserId.Value)
                .Select(g => new
                {
                    UserId = g.Key,
                    Last = g.Max(x => x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.UserId, x => x.Last);
        }

        var signups = recent
            .Select(x => new
            {
                createdAt = x.CreatedAt,
                retained =
                    lastLogins.TryGetValue(x.Id, out var last) &&
                    last.Date > x.CreatedAt.Date
            })
            .ToList();

        var result = new
        {
            totalUsers,
            signups
        };

        _cache.Set(CacheKey, result, TimeSpan.FromSeconds(30));

        return Ok(result);
    }
}
