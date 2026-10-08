using Examo.DTOs.Student;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IStudentService
{
    Task<List<Student>> GetAllAsync();

    Task<Student?> GetByIdAsync(int id);

    Task<Student> CreateAsync(StudentCreateDto dto);

    Task<Student?> UpdateAsync(
        int id,
        StudentUpdateDto dto);

    Task<bool> DeleteAsync(int id);

    // Admin: kisi bhi user ka password reset
    Task<Student?> ResetPasswordAsync(
        int id,
        string newPassword);

    // Admin: user ko block / unblock
    Task<Student?> SetBlockedAsync(
        int id,
        bool isBlocked);
}