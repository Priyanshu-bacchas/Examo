using Examo.DTOs.Student;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class StudentService : IStudentService
{
    private const int MinPasswordLength = 6;

    private readonly IStudentRepository _repository;

    public StudentService(
        IStudentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Student>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Student> CreateAsync(
        StudentCreateDto dto)
    {
        var name = dto.Name.Trim();

        var email =
            dto.Email.Trim().ToLowerInvariant();

        var mobile =
            string.IsNullOrWhiteSpace(
                dto.MobileNumber)
                    ? null
                    : dto.MobileNumber.Trim();

        var role =
            string.IsNullOrWhiteSpace(dto.Role)
                ? "Student"
                : dto.Role.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException(
                "Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Email is required.");

        if (role != "Student" &&
            role != "Admin")
        {
            throw new InvalidOperationException(
                "Role must be Student or Admin.");
        }

        ValidatePassword(dto.Password);

        if (await _repository.EmailExistsAsync(email))
        {
            throw new InvalidOperationException(
                "A student with this email already exists.");
        }

        if (!string.IsNullOrWhiteSpace(mobile) &&
            await _repository.MobileExistsAsync(mobile))
        {
            throw new InvalidOperationException(
                "A student with this mobile number already exists.");
        }

        var student = new Student
        {
            Name = name,
            MobileNumber = mobile,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow,

            // Login password (hash nahi). Khali = abhi login nahi.
            Password = string.IsNullOrWhiteSpace(dto.Password)
                ? string.Empty
                : dto.Password,

            PasswordHash = string.Empty
        };

        return await _repository.AddAsync(student);
    }

    public async Task<Student?> UpdateAsync(
        int id,
        StudentUpdateDto dto)
    {
        var student =
            await _repository.GetByIdAsync(id);

        if (student == null)
            return null;

        var name = dto.Name.Trim();

        var email =
            dto.Email.Trim().ToLowerInvariant();

        var mobile =
            string.IsNullOrWhiteSpace(
                dto.MobileNumber)
                    ? null
                    : dto.MobileNumber.Trim();

        var role =
            string.IsNullOrWhiteSpace(dto.Role)
                ? student.Role
                : dto.Role.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException(
                "Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Email is required.");

        if (role != "Student" &&
            role != "Admin")
        {
            throw new InvalidOperationException(
                "Role must be Student or Admin.");
        }

        ValidatePassword(dto.Password);

        if (await _repository.EmailExistsAsync(
            email,
            id))
        {
            throw new InvalidOperationException(
                "A student with this email already exists.");
        }

        if (!string.IsNullOrWhiteSpace(mobile) &&
            await _repository.MobileExistsAsync(
                mobile,
                id))
        {
            throw new InvalidOperationException(
                "A student with this mobile number already exists.");
        }

        student.Name = name;
        student.MobileNumber = mobile;
        student.Email = email;
        student.Role = role;

        // Sirf tab badlo jab naya password diya ho
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            student.Password = dto.Password;

            // Purana hash hata do, warna purana password bhi chalta rahega
            student.PasswordHash = string.Empty;
        }

        return await _repository.UpdateAsync(
            student);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task<Student?> SetBlockedAsync(
        int id,
        bool isBlocked)
    {
        var student =
            await _repository.GetByIdAsync(id);

        if (student == null)
            return null;

        student.IsBlocked = isBlocked;

        return await _repository.UpdateAsync(student);
    }

    public async Task<Student?> ResetPasswordAsync(
        int id,
        string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new InvalidOperationException(
                "New password is required.");

        if (newPassword.Length < MinPasswordLength)
        {
            throw new InvalidOperationException(
                $"Password must be at least {MinPasswordLength} characters.");
        }

        var student =
            await _repository.GetByIdAsync(id);

        if (student == null)
            return null;

        student.Password = newPassword;

        // Purana hash hata do, warna purana password bhi chalta rahega
        student.PasswordHash = string.Empty;

        return await _repository.UpdateAsync(student);
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return; // optional

        if (password.Length < MinPasswordLength)
        {
            throw new InvalidOperationException(
                $"Password must be at least {MinPasswordLength} characters.");
        }
    }
}