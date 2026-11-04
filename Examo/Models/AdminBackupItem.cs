using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Examo.Models;

// Admin > Backup: Content Manager / NotifyMe / Help & Support se delete hua data.
// Delete se pehle poora record JSON me yahan save hota hai, taaki wapas restore ho sake.
[Table("AdminBackupItems")]
public class AdminBackupItem
{
    public int Id { get; set; }

    // content | notifyme | help
    [MaxLength(20)]
    public string Section { get; set; } = string.Empty;

    // Job | Result | Admit Card | Exam | Help message | Help notification | Subscription notification
    [MaxLength(40)]
    public string ItemType { get; set; } = string.Empty;

    // Delete hone se pehle us record ki Id
    public int? OriginalId { get; set; }

    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(600)]
    public string Summary { get; set; } = string.Empty;

    // Poore record ki copy (JSON)
    public string DataJson { get; set; } = string.Empty;

    // Kis admin ne delete kiya
    [MaxLength(150)]
    public string? DeletedBy { get; set; }

    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
