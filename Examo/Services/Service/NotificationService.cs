using Examo.Models;
using Examo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

public class NotificationService : INotificationService
{
    // Notification types (frontend inhi par icon / page decide karta hai)
    public const string TypeFormWeek = "form-week";   // last date se 1 hafta pehle
    public const string TypeFormEve = "form-eve";     // last date se 1 din pehle
    public const string TypeFormLast = "form-last";   // last date wale din
    public const string TypeExamWeek = "exam-week";
    public const string TypeExamEve = "exam-eve";
    public const string TypeExamDay = "exam-day";
    public const string TypeResult = "result";

    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ExamoDbContext context,
        ICurrentUserService currentUser,
        IEmailService emailService,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    // =====================================================
    // SETTINGS (appsettings / Render env vars)
    //   Notifications__SendHour         default 6   (subah 6 baje)
    //   Notifications__UtcOffsetMinutes default 330 (India, IST = UTC+5:30)
    //   Notifications__SendEmail        default true (in-app ke saath email bhi)
    // =====================================================

    private int SendHour =>
        Math.Clamp(_config.GetValue("Notifications:SendHour", 6), 0, 23);

    private int UtcOffsetMinutes =>
        _config.GetValue("Notifications:UtcOffsetMinutes", 330);

    private bool SendEmail =>
        _config.GetValue("Notifications:SendEmail", true);

    // =====================================================
    // LOGGED-IN STUDENT: list / read / delete
    // =====================================================

    public async Task<List<Notification>> GetMineAsync(
        bool unreadOnly,
        int take)
    {
        var userId = _currentUser.UserId;

        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (unreadOnly)
            query = query.Where(x => !x.IsRead);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == userId && !x.IsDeleted && !x.IsRead);
    }

    public async Task<bool> MarkReadAsync(int id)
    {
        var userId = _currentUser.UserId;

        var item = await _context.Notifications
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId && !x.IsDeleted);

        if (item == null)
            return false;

        if (!item.IsRead)
        {
            item.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync()
    {
        var userId = _currentUser.UserId;

        var unread = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsDeleted && !x.IsRead)
            .ToListAsync();

        foreach (var item in unread)
            item.IsRead = true;

        if (unread.Count > 0)
            await _context.SaveChangesAsync();

        return unread.Count;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var item = await _context.Notifications
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId && !x.IsDeleted);

        if (item == null)
            return false;

        // Row delete nahi hoti, sirf chhupti hai (reminder dobara na bane)
        item.IsDeleted = true;
        item.IsRead = true;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> ClearAllAsync()
    {
        var userId = _currentUser.UserId;

        var all = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .ToListAsync();

        if (all.Count == 0)
            return 0;

        foreach (var item in all)
        {
            item.IsDeleted = true;
            item.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return all.Count;
    }

    // =====================================================
    // RESULT AANE PAR
    // =====================================================

    public async Task NotifyResultAsync(ExamResult result)
    {
        if (result.UserId == null)
            return;

        if (string.Equals(
                result.Result,
                "Awaited",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var user = await _context.Students
            .AsNoTracking()
            .Where(x => x.Id == result.UserId.Value)
            .Select(x => new UserInfo(x.Id, x.Name, x.Email))
            .FirstOrDefaultAsync();

        if (user == null)
            return;

        var qualified = string.Equals(
            result.Result,
            "Qualified",
            StringComparison.OrdinalIgnoreCase);

        var message = qualified
            ? $"Congratulations! You are Qualified in {result.ExamName}."
            : $"Your {result.ExamName} result is out: {result.Result}. " +
              "Don't lose hope, keep preparing!";

        var candidate = new Candidate(
            user.Id,
            TypeResult,
            $"Result declared: {result.ExamName}",
            message,
            result.ExamName,
            // Har baar result badalne par nayi notification
            $"result:{result.Id}:{result.Result}:{DateTime.UtcNow.Ticks}");

        var created = await TryCreateAsync(candidate);

        if (created != null)
            await TrySendEmailAsync(created, user);
    }

    // =====================================================
    // SCHEDULER: Exam Form last date aur Exam date, dono ke liye same rules:
    //   - 1 hafta pehle, 1 din pehle, aur usi din
    //   - Sab SendHour (default subah 6 baje IST) ke baad bante hain
    //   - Ek hi reminder dobara nahi banta (DedupeKey)
    // =====================================================

    public async Task<NotificationRunSummary> RunDueRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var nowLocal = DateTime.UtcNow.AddMinutes(UtcOffsetMinutes);

        // Subah 6 baje se pehle kuch nahi (DB ko bhi nahi chhuta)
        if (nowLocal.Hour < SendHour)
            return new NotificationRunSummary(0, 0, true);

        var today = DateOnly.FromDateTime(nowLocal);
        var windowEnd = today.AddDays(7);

        var candidates = new List<Candidate>();

        // ---- 1. Exam Form (sirf Pending): last date se 1 hafta / 1 din pehle / usi din ----
        // Late add hua form ho (jaise last date 3 din baad hai) tab bhi
        // "N days left" ki ek reminder aa jaati hai.
        var forms = await _context.ExamForms
            .AsNoTracking()
            .Where(x =>
                x.UserId != null &&
                x.Status != "Filled" &&
                x.RegisterEndDate >= today &&
                x.RegisterEndDate <= windowEnd)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.ExamName,
                x.RegisterEndDate
            })
            .ToListAsync(cancellationToken);

        foreach (var form in forms)
        {
            candidates.Add(BuildFormReminder(
                form.UserId!.Value,
                form.Id,
                form.ExamName,
                form.RegisterEndDate,
                form.RegisterEndDate.DayNumber - today.DayNumber));
        }

        // ---- 2. Exams (Done nahi): exam se 1 hafta / 1 din pehle / usi din ----
        var exams = await _context.Exams
            .AsNoTracking()
            .Where(x =>
                x.UserId != null &&
                x.ExamDate != null &&
                x.Status != "Done" &&
                x.ExamDate >= today &&
                x.ExamDate <= windowEnd)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.ExamName,
                x.ExamDate
            })
            .ToListAsync(cancellationToken);

        foreach (var exam in exams)
        {
            var date = exam.ExamDate!.Value;

            candidates.Add(BuildExamReminder(
                exam.UserId!.Value,
                exam.Id,
                exam.ExamName,
                date,
                date.DayNumber - today.DayNumber));
        }

        if (candidates.Count == 0)
            return new NotificationRunSummary(0, 0, false);

        // ---- Sirf active students (blocked / admin nahi) ----
        var userIds = candidates
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var users = await _context.Students
            .AsNoTracking()
            .Where(x =>
                userIds.Contains(x.Id) &&
                !x.IsBlocked &&
                x.Role.ToLower() != "admin")
            .Select(x => new UserInfo(x.Id, x.Name, x.Email))
            .ToListAsync(cancellationToken);

        var userMap = users.ToDictionary(x => x.Id);

        candidates = candidates
            .Where(x => userMap.ContainsKey(x.UserId))
            .ToList();

        // ---- Jo pehle ban chuki hain unhe hatao ----
        var keys = candidates
            .Select(x => x.Key)
            .Distinct()
            .ToList();

        var existing = await _context.Notifications
            .AsNoTracking()
            .Where(x => keys.Contains(x.DedupeKey))
            .Select(x => new { x.UserId, x.DedupeKey })
            .ToListAsync(cancellationToken);

        var existingSet = existing
            .Select(x => (x.UserId, x.DedupeKey))
            .ToHashSet();

        var fresh = candidates
            .Where(x => !existingSet.Contains((x.UserId, x.Key)))
            .ToList();

        var createdCount = 0;
        var emailCount = 0;

        foreach (var candidate in fresh)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var created = await TryCreateAsync(candidate);

            if (created == null)
                continue;

            createdCount++;

            if (await TrySendEmailAsync(created, userMap[candidate.UserId]))
                emailCount++;
        }

        if (createdCount > 0)
        {
            _logger.LogInformation(
                "Notifications created: {Created}, emails sent: {Emails}",
                createdCount,
                emailCount);
        }

        return new NotificationRunSummary(
            createdCount,
            emailCount,
            false);
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private sealed record UserInfo(int Id, string Name, string Email);

    private sealed record Candidate(
        int UserId,
        string Type,
        string Title,
        string Message,
        string? ExamName,
        string Key);

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd MMM yyyy");

    // daysLeft: 0 = usi din, 1 = kal, 2 se 7 = ek hafta pehle wali reminder
    private static Candidate BuildFormReminder(
        int userId,
        int id,
        string name,
        DateOnly date,
        int daysLeft)
    {
        var dateText = FormatDate(date);
        var stamp = date.ToString("yyyyMMdd");

        if (daysLeft == 0)
        {
            return new Candidate(
                userId,
                TypeFormLast,
                $"Last date today: {name}",
                $"Today ({dateText}) is the last date to fill the " +
                $"{name} form. Submit it before the day ends!",
                name,
                $"form-last:{id}:{stamp}");
        }

        if (daysLeft == 1)
        {
            return new Candidate(
                userId,
                TypeFormEve,
                $"Last date tomorrow: {name}",
                $"The last date to fill the {name} form is " +
                $"tomorrow ({dateText}). Submit it on time!",
                name,
                $"form-eve:{id}:{stamp}");
        }

        return new Candidate(
            userId,
            TypeFormWeek,
            daysLeft == 7
                ? $"1 week left: {name}"
                : $"{daysLeft} days left: {name}",
            $"The last date to fill the {name} form is {dateText}, " +
            (daysLeft == 7
                ? "just 1 week away. "
                : $"only {daysLeft} days away. ") +
            "Don't forget to submit it!",
            name,
            $"form-week:{id}:{stamp}");
    }

    private static Candidate BuildExamReminder(
        int userId,
        int id,
        string name,
        DateOnly date,
        int daysLeft)
    {
        var dateText = FormatDate(date);
        var stamp = date.ToString("yyyyMMdd");

        if (daysLeft == 0)
        {
            return new Candidate(
                userId,
                TypeExamDay,
                $"Your exam is today: {name}",
                $"Today is your {name} exam. " +
                "All the best, you've got this!",
                name,
                $"exam-day:{id}:{stamp}");
        }

        if (daysLeft == 1)
        {
            return new Candidate(
                userId,
                TypeExamEve,
                $"Exam tomorrow: {name}",
                $"Your {name} exam is tomorrow ({dateText}). " +
                "Keep your admit card and documents ready, " +
                "and get a good night's sleep.",
                name,
                $"exam-eve:{id}:{stamp}");
        }

        return new Candidate(
            userId,
            TypeExamWeek,
            daysLeft == 7
                ? $"1 week to go: {name}"
                : $"{daysLeft} days to go: {name}",
            $"Your {name} exam is on {dateText}, " +
            (daysLeft == 7
                ? "just 1 week away. "
                : $"only {daysLeft} days away. ") +
            "Time to revise!",
            name,
            $"exam-week:{id}:{stamp}");
    }

    // Notification save karta hai. Duplicate (unique index) ho to null deta hai.
    private async Task<Notification?> TryCreateAsync(Candidate c)
    {
        var item = new Notification
        {
            UserId = c.UserId,
            Type = c.Type,
            Title = Trim(c.Title, 200),
            Message = Trim(c.Message, 1000),
            ExamName = c.ExamName == null ? null : Trim(c.ExamName, 200),
            DedupeKey = Trim(c.Key, 150),
            IsRead = false,
            EmailSent = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.Notifications.Add(item);
            await _context.SaveChangesAsync();

            return item;
        }
        catch (DbUpdateException ex)
        {
            // Kisi aur ne pehle hi bana di (ya DB error): is notification ko skip karo
            _context.ChangeTracker.Clear();

            _logger.LogWarning(
                "Notification skipped ({Key}): {Message}",
                c.Key,
                ex.InnerException?.Message ?? ex.Message);

            return null;
        }
    }

    // Email bhejta hai (Resend). Fail ho to sirf log hota hai, notification rehti hai.
    private async Task<bool> TrySendEmailAsync(
        Notification item,
        UserInfo user)
    {
        if (!SendEmail || string.IsNullOrWhiteSpace(user.Email))
            return false;

        try
        {
            await _emailService.SendNotificationEmailAsync(
                user.Email,
                user.Name,
                item.Title,
                item.Message);

            item.EmailSent = true;
            _context.Notifications.Update(item);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Notification email failed for user {UserId}: {Message}",
                user.Id,
                ex.Message);

            return false;
        }
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
