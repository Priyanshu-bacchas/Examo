using Examo.Models;

using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

// Admin NotifyMe me notification likhne ka chhota helper.
// Notification na bane to bhi asli kaam (support message / payment) par koi asar nahi padta.
public static class AdminNotificationWriter
{
    public const string TypeSupport = "support";
    public const string TypeSubscription = "subscription";

    public static async Task AddAsync(
        ExamoDbContext db,
        string type,
        string title,
        string message,
        int? userId,
        string? userName,
        string? userEmail,
        int? refId,
        string dedupeKey,
        ILogger? logger = null)
    {
        var item = new AdminNotification
        {
            Type = type,
            Title = Cut(title, 200),
            Message = Cut(message, 1000),
            UserId = userId,
            UserName = userName == null ? null : Cut(userName, 150),
            UserEmail = userEmail == null ? null : Cut(userEmail, 200),
            RefId = refId,
            DedupeKey = Cut(dedupeKey, 100),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            db.AdminNotifications.Add(item);

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            db.Entry(item).State = EntityState.Detached;

            logger?.LogError(ex, "Admin notification could not be saved.");
        }
    }

    private static string Cut(string text, int max)
    {
        text ??= string.Empty;

        return text.Length <= max ? text : text[..max];
    }
}
