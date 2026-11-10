using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Examo.Models;

[Table("Ebooks")]
public class Ebook
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string Access { get; set; } = "Free";

    // true = Draft; false = Published
    public bool IsDraft { get; set; }

    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
