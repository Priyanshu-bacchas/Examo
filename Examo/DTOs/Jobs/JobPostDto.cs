using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Jobs;

// Admin form se aata hai (Job / Result / AdmitCard teeno ke liye)
public class JobPostDto
{
    [Required]
    [RegularExpression(
        "^(Job|Result|AdmitCard)$",
        ErrorMessage = "Category must be Job, Result or AdmitCard.")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    // Job
    public DateOnly? StartDate { get; set; }

    public DateOnly? LastDate { get; set; }

    public DateOnly? ExamDate { get; set; }

    [MaxLength(5000)]
    public string? Syllabus { get; set; }

    // Result / AdmitCard
    public DateOnly? PostDate { get; set; }

    [MaxLength(30)]
    public string? Status { get; set; }

    [Required]
    [MaxLength(500)]
    public string Link { get; set; } = string.Empty;
}
