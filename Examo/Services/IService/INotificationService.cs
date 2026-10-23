using Examo.Models;

namespace Examo.Services.Interfaces;

// Scheduler ek baar chalne ke baad ka summary
public record NotificationRunSummary(
    int Created,
    int EmailsSent,
    bool Skipped);

public interface INotificationService
{
    // ---------- Logged-in student ----------
    Task<List<Notification>> GetMineAsync(bool unreadOnly, int take);

    Task<int> GetUnreadCountAsync();

    Task<bool> MarkReadAsync(int id);

    Task<int> MarkAllReadAsync();

    Task<bool> DeleteAsync(int id);

    Task<int> ClearAllAsync();

    // ---------- Result aane par (ExamResultService se) ----------
    Task NotifyResultAsync(ExamResult result);

    // ---------- Scheduler: form last date + exam reminders ----------
    Task<NotificationRunSummary> RunDueRemindersAsync(
        CancellationToken cancellationToken = default);
}
