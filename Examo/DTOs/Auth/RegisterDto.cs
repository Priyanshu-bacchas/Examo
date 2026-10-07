using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Auth;

public class RegisterDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    // Mobile Number
    [Required]
    [MaxLength(50)]
    public string StudentId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}