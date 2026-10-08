namespace Examo.Models;

// Logs & Security: kisne kab login kiya / kya change kiya
public class ActivityLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? UserEmail { get; set; }

    public string? Role { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public bool Success { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
