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
}