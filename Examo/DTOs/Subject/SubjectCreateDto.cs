using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Subject;

public class SubjectCreateDto
{
    [Required]
    [MaxLength(150)]
    public string SubjectName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Not Started|In Progress|Completed)$",
        ErrorMessage =
            "Status must be Not Started, In Progress, or Completed.")]
    public string Status { get; set; } = "Not Started";

    public string? Materials { get; set; }

    [Range(0, int.MaxValue)]
    public int Lectures { get; set; }

    [MaxLength(500)]
    public string? Pdf { get; set; }

    [MaxLength(500)]
    public string? Link { get; set; }
}