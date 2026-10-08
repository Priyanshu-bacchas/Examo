using System;
using System.Collections.Generic;

namespace Examo.Models;

public partial class Schedule
{
    public int Id { get; set; }

    public string Subject { get; set; } = null!;

    public DateOnly ScheduleDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Description { get; set; }

    public int? Lecture { get; set; }

    public DateTime CreatedAt { get; set; }

    // Is record ka owner (Students.Id). Har user sirf apna data dekhta hai.
    public int? UserId { get; set; }
}