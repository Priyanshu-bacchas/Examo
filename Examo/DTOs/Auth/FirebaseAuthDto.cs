using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Auth;

public class FirebaseAuthDto
{
    [Required]
    public string IdToken { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? StudentId { get; set; }
}