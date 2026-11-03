using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Examo.Models;

// Admin ke NotifyMe section ki notifications:
//   support       -> student ne Help > Contact Us se message bheja
//   subscription  -> student ne subscription (paid plan) liya
[Table("AdminNotifyItems")]
public class AdminNotification
{
    public int Id { get; set; }

    // support | subscription
    [MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    // Kis student ki wajah se notification bani (Students.Id)
    public int? UserId { get; set; }

    [MaxLength(150)]
    public string? UserName { get; set; }

    [MaxLength(200)]
    public string? UserEmail { get; set; }

    // support -> SupportMessages.Id, subscription -> Payments.Id
    public int? RefId { get; set; }

    // Duplicate rokne ke liye (unique)
    [MaxLength(100)]
    public string DedupeKey { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}