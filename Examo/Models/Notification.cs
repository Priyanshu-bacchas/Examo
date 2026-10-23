using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Examo.Models;

// Student ki in-app notifications (exam form last date, exam reminders, result)
[Table("Notifications")]
public class Notification
{
    public int Id { get; set; }

    // Kis student ke liye (Students.Id)
    public int UserId { get; set; }

    // form-deadline | exam-week | exam-eve | exam-day | result
    [MaxLength(40)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ExamName { get; set; }

    // Duplicate rokne ke liye (UserId + DedupeKey unique hai)
    [MaxLength(150)]
    public string DedupeKey { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public bool EmailSent { get; set; }

    // Student ne delete kiya to row hatti nahi, sirf chhup jaati hai.
    // Isse scheduler wahi reminder dobara nahi bana paata (aur email dobara nahi jaati).
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
