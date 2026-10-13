namespace Examo.Models;

// Results section: har exam ka result (Awaited / Qualified / Not Qualified)
public partial class ExamResult
{
    public int Id { get; set; }

    public string ExamName { get; set; } = null!;

    public string Result { get; set; } = "Awaited";

    public DateTime CreatedAt { get; set; }

    // Owner (Students.Id)
    public int? UserId { get; set; }
}
