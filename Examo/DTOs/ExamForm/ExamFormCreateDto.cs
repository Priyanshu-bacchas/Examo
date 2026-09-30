using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.ExamForm;

public class ExamFormCreateDto
{
    [Required]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    [Required]
    public DateOnly RegisterStartDate { get; set; }

    [Required]
    public DateOnly RegisterEndDate { get; set; }

    [MaxLength(500)]
    public string? Link { get; set; }

    [Required]
    [RegularExpression(
        "^(Filled|Pending)$",
        ErrorMessage = "Status must be Filled or Pending.")]
    public string Status { get; set; } = "Pending";
}