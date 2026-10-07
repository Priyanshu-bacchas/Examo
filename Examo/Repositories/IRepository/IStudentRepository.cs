using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync();

    Task<Student?> GetByIdAsync(int id);

    Task<Student?> GetByEmailAsync(string email);

    Task<Student?> GetByMobileNumberAsync(string mobileNumber);

    Task<Student?> GetByIdentifierAsync(string identifier);

    Task<Student> AddAsync(Student student);

    Task<Student> UpdateAsync(Student student);

    Task<bool> DeleteAsync(int id);

    Task<bool> EmailExistsAsync(
        string email,
        int? excludeId = null);

    Task<bool> MobileExistsAsync(
        string mobileNumber,
        int? excludeId = null);
}