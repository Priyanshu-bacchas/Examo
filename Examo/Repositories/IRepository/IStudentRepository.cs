using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IStudentRepository
{
    Task<IEnumerable<Student>> GetAllAsync();
    Task<Student?> GetByIdAsync(int id);
    Task<Student> AddAsync(Student student);
    Task<Student> UpdateAsync(Student student);
    Task<bool> DeleteAsync(int id);
}