using Examo.DTOs.Result;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IExamResultService
{
    Task<IEnumerable<ExamResult>> GetAllAsync();
    Task<ExamResult?> GetByIdAsync(int id);
    Task<ExamResult> AddAsync(ResultCreateDto dto);
    Task<ExamResult?> UpdateAsync(int id, ResultUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}
