using System;

namespace Examo.Models;

public partial class Preparation
{
    public int Id { get; set; }

    public string ExamName { get; set; } = null!;

    public string? Syllabus { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    // Is record ka owner (Students.Id). Har user sirf apna data dekhta hai.
    public int? UserId { get; set; }

    // Agar ye record Admin ki Job se "Add to ExamForm" karke bana hai,
    // to us Job (JobPosts.Id) ka Id. Admin job edit/delete kare to
    // isi se user ke records update/delete hote hain.
    public int? SourceJobId { get; set; }
}
