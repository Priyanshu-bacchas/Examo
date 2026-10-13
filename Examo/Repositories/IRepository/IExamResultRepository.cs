using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IExamResultRepository
{
    Task<IEnumerable<ExamResult>> GetAllAsync();
    Task<ExamResult?> GetByIdAsync(int id);
    Task<ExamResult> AddAsync(ExamResult result);
    Task<ExamResult> UpdateAsync(ExamResult result);
    Task<bool> DeleteAsync(int id);
}
