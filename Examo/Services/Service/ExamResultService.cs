using Examo.DTOs.Result;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

public class ExamResultService : IExamResultService
{
    private readonly IExamResultRepository _repository;
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExamResultService(
        IExamResultRepository repository,
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<ExamResult>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        // Is user ke Exam Forms ke saare exam names Results me sync karo
        var examFormNames = await _context.ExamForms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ExamName)
            .Distinct()
            .ToListAsync();

        var existingNames = await _context.Results
            .Where(x => x.UserId == userId)
            .Select(x => x.ExamName)
            .ToListAsync();

        foreach (var examName in examFormNames)
        {
            if (!existingNames.Contains(examName))
            {
                await _context.Results.AddAsync(new ExamResult
                {
                    ExamName = examName,
                    Result = "Awaited",
                    UserId = userId
                });
            }
        }

        await _context.SaveChangesAsync();

        return await _repository.GetAllAsync();
    }

    public async Task<ExamResult?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<ExamResult> AddAsync(ResultCreateDto dto)
    {
        var result = new ExamResult
        {
            ExamName = dto.ExamName,
            Result = dto.Result
        };

        return await _repository.AddAsync(result);
    }

    public async Task<ExamResult?> UpdateAsync(
        int id,
        ResultUpdateDto dto)
    {
        var result = await _repository.GetByIdAsync(id);

        if (result == null)
        {
            return null;
        }

        result.ExamName = dto.ExamName;
        result.Result = dto.Result;

        return await _repository.UpdateAsync(result);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}
