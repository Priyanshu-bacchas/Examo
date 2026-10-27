using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Jobs;

// Admin form se aata hai (Job / Result / AdmitCard teeno ke liye)
public class JobPostDto
{
    [Required]
    [RegularExpression(
        "^(Job|Result|AdmitCard|Exam)$",
        ErrorMessage = "Category must be Job, Result, AdmitCard or Exam.")]
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

    // Job / Result / AdmitCard (optional): YouTube video link
    [MaxLength(500)]
    public string? Details { get; set; }

    // Result / AdmitCard
    public DateOnly? PostDate { get; set; }

    [MaxLength(30)]
    public string? Status { get; set; }

    // "Published" (default) ya "Draft"
    [MaxLength(20)]
    public string? PublishStatus { get; set; }

    // Exam category me Link nahi hota (Validate me check hota hai)
    [MaxLength(500)]
    public string Link { get; set; } = string.Empty;
}

public class JobPublishDto
{
    // true = Publish (students ko dikhe), false = Draft (chhupa do)
    public bool Published { get; set; }
}
