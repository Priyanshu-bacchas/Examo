using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IExamFormRepository
{
    Task<IEnumerable<ExamForm>> GetAllAsync();
    Task<ExamForm?> GetByIdAsync(int id);
    Task<ExamForm> AddAsync(ExamForm examForm);
    Task<ExamForm> UpdateAsync(ExamForm examForm);
    Task<bool> DeleteAsync(int id);
}