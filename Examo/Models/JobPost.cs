using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Examo.Models;

// Admin ke dwara manually add kiye gaye Jobs / Results / Admit Cards.
// Teeno ek hi table me hain, Category se alag hote hain.
[Table("JobPosts")]
public class JobPost
{
    public const string CategoryJob = "Job";
    public const string CategoryResult = "Result";
    public const string CategoryAdmitCard = "AdmitCard";

    public int Id { get; set; }

    // Job | Result | AdmitCard
    [MaxLength(20)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    // Sirf Job ke liye
    public DateOnly? StartDate { get; set; }

    public DateOnly? LastDate { get; set; }

    // Sirf Job ke liye (optional): exam ki date aur syllabus
    public DateOnly? ExamDate { get; set; }

    [MaxLength(5000)]
    public string? Syllabus { get; set; }

    // Sirf Result / AdmitCard ke liye
    public DateOnly? PostDate { get; set; }

    // Sirf Result / AdmitCard ke liye: Out | Upcoming
    [MaxLength(30)]
    public string? Status { get; set; }

    [MaxLength(500)]
    public string Link { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
