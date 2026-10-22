namespace Examo.Models;

// Help > Contact Us se aaya message (Admin > Help & Support me dikhta hai)
public class SupportMessage
{
    public int Id { get; set; }

    // Kis logged-in user ne bheja
    public int? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // "Open" ya "Resolved"
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    // Admin ka bheja hua (latest) reply, email se student ko gaya
    public string? AdminReply { get; set; }

    public DateTime? RepliedAt { get; set; }

    // Kis admin ne reply kiya (naam)
    public string? RepliedBy { get; set; }
}