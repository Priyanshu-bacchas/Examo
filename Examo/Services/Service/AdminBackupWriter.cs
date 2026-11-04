using System.Text.Json;

using Examo.Models;

using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

// Delete se pehle record ki copy Backup me rakhne ka helper.
// Stage() sirf context me add karta hai; delete wale SaveChanges ke saath hi save hota hai,
// isliye "delete hua par backup nahi bana" wali halat nahi banti.
public static class AdminBackupWriter
{
    public const string SectionContent = "content";
    public const string SectionNotifyMe = "notifyme";
    public const string SectionHelp = "help";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static AdminBackupItem Stage(
        ExamoDbContext db,
        string section,
        string itemType,
        int? originalId,
        string title,
        string summary,
        object data,
        string? deletedBy)
    {
        var item = new AdminBackupItem
        {
            Section = section,
            ItemType = Cut(itemType, 40),
            OriginalId = originalId,
            Title = Cut(title, 300),
            Summary = Cut(summary, 600),
            DataJson = JsonSerializer.Serialize(data, data.GetType(), JsonOptions),
            DeletedBy = deletedBy == null ? null : Cut(deletedBy, 150),
            DeletedAt = DateTime.UtcNow
        };

        db.AdminBackupItems.Add(item);

        return item;
    }

    // Delete karne wale admin ka naam
    public static async Task<string?> GetAdminNameAsync(
        ExamoDbContext db,
        int? userId)
    {
        if (userId == null)
            return null;

        return await db.Students
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();
    }

    public static string Cut(string? text, int max)
    {
        text ??= string.Empty;

        return text.Length <= max ? text : text[..max];
    }
}
