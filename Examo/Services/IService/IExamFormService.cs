using Examo.DTOs.ExamForm;
using Examo.Models;

namespace Examo.Services.Interfaces;

public interface IExamFormService
{
    Task<IEnumerable<ExamForm>> GetAllAsync();
    Task<ExamForm?> GetByIdAsync(int id);
    Task<ExamForm> AddAsync(ExamFormCreateDto dto);
    Task<ExamForm?> UpdateAsync(int id, ExamFormUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}