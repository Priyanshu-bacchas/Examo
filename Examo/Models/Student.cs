using System.Text.Json.Serialization;

namespace Examo.Models;

public class Student
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MobileNumber { get; set; }

    public string Email { get; set; } = string.Empty;

    // Purana column (DB me NOT NULL hai, isliye rakha hai). Ab use nahi hota.
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    // User ka login password (plain). Entity serialize hone par bahar nahi jaata;
    // admin ko sirf StudentResponseDto ke through dikhta hai.
    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Student";

    // Admin ne block kiya ho to user login nahi kar sakta
    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // =====================================================
    // EMAIL OTP (verification + forgot password)
    // =====================================================

    // Default TRUE: admin-created / Google / purane users verified maane jaate hain.
    // Sirf public Register me false set hota hai.
    public bool EmailVerified { get; set; } = true;

    // OTP ka HMAC hash (plain OTP kabhi DB me nahi jaata)
    [JsonIgnore]
    public string? OtpHash { get; set; }

    [JsonIgnore]
    public DateTime? OtpExpiresAt { get; set; }

    [JsonIgnore]
    public int OtpAttempts { get; set; }

    [JsonIgnore]
    public DateTime? OtpSentAt { get; set; }
}