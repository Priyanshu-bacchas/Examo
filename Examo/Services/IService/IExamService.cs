using Examo.DTOs.Exam;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IExamService
{
    Task<IEnumerable<Exam>> GetAllAsync();
    Task<Exam?> GetByIdAsync(int id);
    Task<Exam> AddAsync(ExamCreateDto dto);
    Task<Exam?> UpdateAsync(int id, ExamUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}