using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Student;

public class StudentUpdateDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MobileNumber { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Role { get; set; } = "Student";

    // Khali / null = purana password waisa hi rahega
    [MaxLength(100)]
    public string? Password { get; set; }
}