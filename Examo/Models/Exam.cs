using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class Exam
{
    public int Id { get; set; }

    public string ExamName { get; set; } = null!;

    public DateOnly? ExamDate { get; set; }

    public string Status { get; set; } = "Coming Soon";

    public DateTime CreatedAt { get; set; }
}