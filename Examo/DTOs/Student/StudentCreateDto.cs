using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Student;

public class StudentCreateDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Course { get; set; }

    public int? Age { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }
}