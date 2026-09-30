using Examo.DTOs.ExamForm;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

namespace Examo.Services;

public class ExamFormService : IExamFormService
{
    private readonly IExamFormRepository _repository;
    private readonly IExamRepository _examRepository;
    private readonly IPreparationRepository _preparationRepository;

    public ExamFormService(
        IExamFormRepository repository,
        IExamRepository examRepository,
        IPreparationRepository preparationRepository)
    {
        _repository = repository;
        _examRepository = examRepository;
        _preparationRepository = preparationRepository;
    }

    public async Task<IEnumerable<ExamForm>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<ExamForm?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<ExamForm> AddAsync(ExamFormCreateDto dto)
    {
        var examForm = new ExamForm
        {
            ExamName = dto.ExamName,
            RegisterStartDate = dto.RegisterStartDate,
            RegisterEndDate = dto.RegisterEndDate,
            Link = dto.Link,
            Status = dto.Status
        };

        var savedExamForm = await _repository.AddAsync(examForm);

        // Automatically create Exam with only ExamName.
        // ExamDate remains NULL and Status remains Coming Soon.
        var exam = new Exam
        {
            ExamName = dto.ExamName,
            ExamDate = null,
            Status = "Coming Soon"
        };

        await _examRepository.AddAsync(exam);

        // Automatically create Preparation with only ExamName.
        var preparation = new Preparation
        {
            ExamName = dto.ExamName,
            Status = "Not Started"
        };

        await _preparationRepository.AddAsync(preparation);

        return savedExamForm;
    }

    public async Task<ExamForm?> UpdateAsync(
        int id,
        ExamFormUpdateDto dto)
    {
        var examForm = await _repository.GetByIdAsync(id);

        if (examForm == null)
        {
            return null;
        }

        examForm.ExamName = dto.ExamName;
        examForm.RegisterStartDate = dto.RegisterStartDate;
        examForm.RegisterEndDate = dto.RegisterEndDate;
        examForm.Link = dto.Link;
        examForm.Status = dto.Status;

        return await _repository.UpdateAsync(examForm);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}