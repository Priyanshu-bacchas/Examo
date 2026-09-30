using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Schedule;

public class ScheduleUpdateDto
{
    [Required]
    [MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public DateOnly ScheduleDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int? Lecture { get; set; }
}