using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Exam;

public class ExamUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    public DateOnly? ExamDate { get; set; }

    [Required]
    [RegularExpression(
        "^(Done|Coming Soon)$",
        ErrorMessage = "Status must be Done or Coming Soon.")]
    public string Status { get; set; } = "Coming Soon";
}