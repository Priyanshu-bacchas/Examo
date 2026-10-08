using System.Security.Claims;

using Examo.Models;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string? details = null,
        int? userId = null,
        string? userName = null,
        string? email = null,
        string? role = null,
        bool success = true)
    {
        try
        {
            var http = _httpContextAccessor.HttpContext;
            var principal = http?.User;

            if (userId == null &&
                int.TryParse(
                    principal?.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var claimId))
            {
                userId = claimId;
            }

            userName ??= principal?.FindFirstValue(ClaimTypes.Name);
            email ??= principal?.FindFirstValue(ClaimTypes.Email);
            role ??= principal?.FindFirstValue(ClaimTypes.Role);

            var log = new ActivityLog
            {
                UserId = userId,
                UserName = Truncate(userName, 150) ?? "Unknown",
                UserEmail = Truncate(email, 200),
                Role = Truncate(role, 20),
                Action = Truncate(action, 100) ?? "Unknown",
                Details = Truncate(details, 1000),
                IpAddress = Truncate(
                    http?.Connection.RemoteIpAddress?.ToString(),
                    64),
                Success = success,
                CreatedAt = DateTime.UtcNow
            };

            // Alag scope = alag DbContext, taaki main request ka
            // context kabhi disturb na ho.
            using var scope = _scopeFactory.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<ExamoDbContext>();

            context.ActivityLogs.Add(log);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log fail hone se request fail nahi honi chahiye
            _logger.LogWarning(
                ex,
                "Activity log could not be saved.");
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= max
            ? value
            : value[..max];
    }
}
