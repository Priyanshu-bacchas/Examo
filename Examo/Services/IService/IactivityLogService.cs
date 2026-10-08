namespace Examo.Services.Interfaces;

public interface IActivityLogService
{
    // userId/userName/email/role null ho to current logged-in user se liye jaate hain.
    Task LogAsync(
        string action,
        string? details = null,
        int? userId = null,
        string? userName = null,
        string? email = null,
        string? role = null,
        bool success = true);
}
