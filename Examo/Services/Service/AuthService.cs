using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Examo.DTOs.Auth;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

using FirebaseAdmin.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Examo.Services;

public class AuthService : IAuthService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<Student> _passwordHasher;

    public AuthService(
        IStudentRepository studentRepository,
        IConfiguration configuration)
    {
        _studentRepository = studentRepository;
        _configuration = configuration;

        _passwordHasher =
            new PasswordHasher<Student>();
    }

    // =========================
    // REGISTER
    // =========================

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterDto dto)
    {
        var fullName =
            dto.FullName?.Trim();

        var mobile =
            dto.StudentId?.Trim();

        var email =
            dto.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException(
                "Full name is required.");

        if (string.IsNullOrWhiteSpace(mobile))
            throw new InvalidOperationException(
                "Mobile number is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new InvalidOperationException(
                "Password is required.");

        if (await _studentRepository.EmailExistsAsync(email))
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        if (await _studentRepository.MobileExistsAsync(mobile))
        {
            throw new InvalidOperationException(
                "An account with this mobile number already exists.");
        }

        var student = new Student
        {
            Name = fullName,
            MobileNumber = mobile,
            Email = email,

            // Public registration is always Student.
            Role = "Student",

            CreatedAt = DateTime.UtcNow
        };

        student.PasswordHash =
            _passwordHasher.HashPassword(
                student,
                dto.Password);

        var savedStudent =
            await _studentRepository.AddAsync(student);

        return new AuthResponseDto
        {
            Token = GenerateToken(savedStudent),
            User = MapStudent(savedStudent)
        };
    }

    // =========================
    // LOGIN
    // =========================

    public async Task<AuthResponseDto?> LoginAsync(
        LoginDto dto)
    {
        var identifier =
            dto.Identifier?.Trim();

        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var student =
            await _studentRepository
                .GetByIdentifierAsync(identifier);

        if (student == null)
            return null;

        if (string.IsNullOrWhiteSpace(
            student.PasswordHash))
        {
            return null;
        }

        var passwordResult =
            _passwordHasher.VerifyHashedPassword(
                student,
                student.PasswordHash,
                dto.Password);

        if (passwordResult ==
            PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // FIREBASE LOGIN
    // =========================

    public async Task<AuthResponseDto>
        FirebaseLoginAsync(FirebaseAuthDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            throw new InvalidOperationException(
                "Firebase ID token is required.");
        }

        FirebaseToken decodedToken;

        try
        {
            decodedToken =
                await FirebaseAuth
                    .DefaultInstance
                    .VerifyIdTokenAsync(dto.IdToken);
        }
        catch
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired Firebase authentication token.");
        }

        var email =
            decodedToken.Claims.TryGetValue(
                "email",
                out var emailValue)
                    ? emailValue?.ToString()
                    : null;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "Firebase account does not contain an email address.");
        }

        email = email.Trim().ToLowerInvariant();

        var firebaseName =
            decodedToken.Claims.TryGetValue(
                "name",
                out var nameValue)
                    ? nameValue?.ToString()
                    : null;

        var student =
            await _studentRepository
                .GetByEmailAsync(email);

        // =========================
        // NEW FIREBASE USER
        // =========================

        if (student == null)
        {
            var fullName =
                !string.IsNullOrWhiteSpace(dto.FullName)
                    ? dto.FullName.Trim()
                    : !string.IsNullOrWhiteSpace(firebaseName)
                        ? firebaseName.Trim()
                        : email.Split('@')[0];

            string? mobile = null;

            if (!string.IsNullOrWhiteSpace(dto.StudentId))
            {
                mobile = dto.StudentId.Trim();

                if (await _studentRepository
                    .MobileExistsAsync(mobile))
                {
                    throw new InvalidOperationException(
                        "An account with this mobile number already exists.");
                }
            }

            student = new Student
            {
                Name = fullName,
                MobileNumber = mobile,
                Email = email,
                Role = "Student",
                CreatedAt = DateTime.UtcNow
            };

            student.PasswordHash =
                _passwordHasher.HashPassword(
                    student,
                    GenerateRandomInternalValue());

            student =
                await _studentRepository
                    .AddAsync(student);
        }
        else
        {
            var changed = false;

            if (!string.IsNullOrWhiteSpace(dto.FullName) &&
                string.IsNullOrWhiteSpace(student.Name))
            {
                student.Name =
                    dto.FullName.Trim();

                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(dto.StudentId) &&
                string.IsNullOrWhiteSpace(student.MobileNumber))
            {
                var mobile =
                    dto.StudentId.Trim();

                if (await _studentRepository
                    .MobileExistsAsync(
                        mobile,
                        student.Id))
                {
                    throw new InvalidOperationException(
                        "An account with this mobile number already exists.");
                }

                student.MobileNumber = mobile;

                changed = true;
            }

            if (changed)
            {
                student =
                    await _studentRepository
                        .UpdateAsync(student);
            }
        }

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // JWT
    // =========================

    private string GenerateToken(
        Student student)
    {
        var jwtKey =
            _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing from configuration.");
        }

        var issuer =
            _configuration["Jwt:Issuer"];

        var audience =
            _configuration["Jwt:Audience"];

        var claims =
            new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    student.Id.ToString()),

                new(
                    ClaimTypes.Name,
                    student.Name),

                new(
                    ClaimTypes.Email,
                    student.Email),

                new(
                    ClaimTypes.Role,
                    student.Role)
            };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires:
                    DateTime.UtcNow.AddDays(7),
                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    // =========================
    // RESPONSE MAPPING
    // =========================

    private static AuthUserDto MapStudent(
        Student student)
    {
        return new AuthUserDto
        {
            Id = student.Id,
            FullName = student.Name,
            StudentId = student.MobileNumber,
            Email = student.Email,
            Role = student.Role
        };
    }

    private static string
        GenerateRandomInternalValue()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
    }
}