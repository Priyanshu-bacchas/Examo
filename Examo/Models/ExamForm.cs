using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class ExamForm
{
    public int Id { get; set; }

    public string ExamName { get; set; } = null!;

    public DateOnly RegisterStartDate { get; set; }

    public DateOnly RegisterEndDate { get; set; }

    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;
}
