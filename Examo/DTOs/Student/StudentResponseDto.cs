using Examo.Models;

namespace Examo.DTOs.Student;

public class StudentResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MobileNumber { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Student";

    public DateTime CreatedAt { get; set; }

    public static StudentResponseDto From(Models.Student s)
    {
        return new StudentResponseDto
        {
            Id = s.Id,
            Name = s.Name,
            MobileNumber = s.MobileNumber,
            Email = s.Email,
            Password = s.Password,
            Role = s.Role,
            CreatedAt = s.CreatedAt
        };
    }
}