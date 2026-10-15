using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Document;

public class DocumentUpdateDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}