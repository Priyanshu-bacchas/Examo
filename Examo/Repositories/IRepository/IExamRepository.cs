using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IExamRepository
{
    Task<IEnumerable<Exam>> GetAllAsync();
    Task<Exam?> GetByIdAsync(int id);
    Task<Exam> AddAsync(Exam exam);
    Task<Exam> UpdateAsync(Exam exam);
    Task<bool> DeleteAsync(int id);
}