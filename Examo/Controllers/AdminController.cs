using Examo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Examo.Controllers;

// System Monitoring & Reports - sirf Admin
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private const string OverviewCacheKey = "admin-overview";

    private readonly ExamoDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    public AdminController(
        ExamoDbContext context,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    // =====================================================
    // DASHBOARD INSIGHTS
    // =====================================================
    //
    // Fast banane ke liye:
    //  - 15+ alag COUNT queries ki jagah sirf 4 queries
    //  - queries parallel chalti hain (har ek ka apna DbContext)
    //  - result 20 second cache hota hai (Refresh button ?fresh=true bhejta hai)

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] bool fresh = false)
    {
        if (!fresh &&
            _cache.TryGetValue(OverviewCacheKey, out object? cached) &&
            cached != null)
        {
            return Ok(cached);
        }

        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last60Days = now.AddDays(-60);

        var userStatsTask = RunAsync(db => db.Students
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Students = g.Count(x => x.Role == "Student"),
                Admins = g.Count(x => x.Role == "Admin"),
                NewUsers = g.Count(x => x.CreatedAt >= last7Days)
            })
            .FirstOrDefaultAsync());

        var logStatsTask = RunAsync(db => db.ActivityLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= last24Hours)
            .GroupBy(x => 1)
            .Select(g => new
            {
                Actions = g.Count(),
                Logins = g.Count(x =>
                    x.Action == "Login" && x.Success),
                Failed = g.Count(x =>
                    x.Action == "Login Failed")
            })
            .FirstOrDefaultAsync());

        var recentTask = RunAsync(db => db.ActivityLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(4)
            .ToListAsync());

        // Registrations chart ke liye sirf dates (passwords etc. nahi)
        var signupsTask = RunAsync(db => db.Students
            .AsNoTracking()
            .Where(x => x.CreatedAt >= last60Days)
            .Select(x => x.CreatedAt)
            .ToListAsync());

        await Task.WhenAll(
            userStatsTask,
            logStatsTask,
            recentTask,
            signupsTask);

        var userStats = userStatsTask.Result;
        var logStats = logStatsTask.Result;

        var result = new
        {
            totalUsers = userStats?.Total ?? 0,
            totalStudents = userStats?.Students ?? 0,
            totalAdmins = userStats?.Admins ?? 0,
            newUsersLast7Days = userStats?.NewUsers ?? 0,
            loginsLast24Hours = logStats?.Logins ?? 0,
            failedLoginsLast24Hours = logStats?.Failed ?? 0,
            actionsLast24Hours = logStats?.Actions ?? 0,
            recentActivity = recentTask.Result,
            signupDates = signupsTask.Result
        };

        _cache.Set(
            OverviewCacheKey,
            result,
            TimeSpan.FromSeconds(20));

        return Ok(result);
    }

    // Har query ko apne scope + DbContext me chalata hai (parallel safe)
    private async Task<T> RunAsync<T>(
        Func<ExamoDbContext, Task<T>> query)
    {
        using var scope = _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<ExamoDbContext>();

        return await query(db);
    }

    // =====================================================
    // LOGS & SECURITY
    // =====================================================

    [HttpGet("logs")]
    public async Task<IActionResult> Logs(
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 1, 500);

        var query =
            _context.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim();

            query = query.Where(x =>
                x.Action == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(x =>
                EF.Functions.ILike(x.UserName, pattern) ||
                (x.UserEmail != null &&
                    EF.Functions.ILike(x.UserEmail, pattern)) ||
                (x.Details != null &&
                    EF.Functions.ILike(x.Details, pattern)));
        }

        var logs =
            await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync();

        return Ok(logs);
    }
}
