using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Preparation;

public class PreparationCreateDto
{
    [Required]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    public string? Syllabus { get; set; }

    [Required]
    [RegularExpression(
        "^(Not Started|In Progress|Completed)$",
        ErrorMessage =
            "Status must be Not Started, In Progress, or Completed.")]
    public string Status { get; set; } = "Not Started";
}