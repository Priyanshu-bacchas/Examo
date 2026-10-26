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
        var exams = (await _repository.GetAllAsync()).ToList();

        // Exam date nikal gayi aur status abhi bhi "Coming Soon" hai
        // to apne aap "Done" ho jata hai.
        var today = ExamStatusHelper.Today();

        foreach (var exam in exams)
        {
            if (exam.ExamDate.HasValue &&
                exam.ExamDate.Value < today &&
                exam.Status != ExamStatusHelper.Done)
            {
                exam.Status = ExamStatusHelper.Done;

                await _repository.UpdateAsync(exam);
            }
        }

        return exams;
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

            // Date di hai to status date se tay hota hai
            Status = dto.ExamDate.HasValue
                ? ExamStatusHelper.FromDate(dto.ExamDate)
                : dto.Status
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

        var dateChanged = exam.ExamDate != dto.ExamDate;

        exam.ExamName = dto.ExamName;
        exam.ExamDate = dto.ExamDate;

        // Date badli hai to status nayi date ke hisaab se;
        // date wahi hai to user ka chuna hua status rahega.
        exam.Status = dateChanged && dto.ExamDate.HasValue
            ? ExamStatusHelper.FromDate(dto.ExamDate)
            : dto.Status;

        return await _repository.UpdateAsync(exam);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}