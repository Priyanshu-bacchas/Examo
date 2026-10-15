using System.Text.Json.Serialization;

namespace Examo.Models;

// Documents section: user ki photo / PDF (file Supabase Storage me, yahan sirf details)
public partial class UserDocument
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string FileName { get; set; } = null!;

    // Supabase bucket ke andar ka path (client ko nahi bhejte)
    [JsonIgnore]
    public string StoragePath { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    // "Photo" ya "PDF"
    public string Kind { get; set; } = "PDF";

    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }
}
