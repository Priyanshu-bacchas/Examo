using Examo.DTOs.Exam;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class ExamService : IExamService
{
    private readonly IExamRepository _repository;

    public ExamService(IExamRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Exam>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Exam?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Exam> AddAsync(ExamCreateDto dto)
    {
        var exam = new Exam
        {
            ExamName = dto.ExamName,
            ExamDate = dto.ExamDate,
            Status = dto.Status
        };

        return await _repository.AddAsync(exam);
    }

    public async Task<Exam?> UpdateAsync(
        int id,
        ExamUpdateDto dto)
    {
        var exam = await _repository.GetByIdAsync(id);

        if (exam == null)
        {
            return null;
        }

        exam.ExamName = dto.ExamName;
        exam.ExamDate = dto.ExamDate;
        exam.Status = dto.Status;

        return await _repository.UpdateAsync(exam);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}