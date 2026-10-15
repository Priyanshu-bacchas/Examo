using Examo.Models;

namespace Examo.DTOs.Document;

public class DocumentResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }

    public static DocumentResponseDto From(UserDocument d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        FileName = d.FileName,
        Kind = d.Kind,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        CreatedAt = d.CreatedAt
    };
}
