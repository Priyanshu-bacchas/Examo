using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Student;

public class ResetPasswordDto
{
    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}
