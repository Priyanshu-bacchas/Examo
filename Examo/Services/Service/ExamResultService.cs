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
    private readonly INotificationService _notifications;

    public ExamResultService(
        IExamResultRepository repository,
        ExamoDbContext context,
        ICurrentUserService currentUser,
        INotificationService notifications)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
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

        var saved = await _repository.AddAsync(result);

        // Result seedha Qualified / Not Qualified ke saath bana ho to notification
        await TryNotifyAsync(saved);

        return saved;
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

        var previousResult = result.Result;

        result.ExamName = dto.ExamName;
        result.Result = dto.Result;

        var updated = await _repository.UpdateAsync(result);

        // Result badla (jaise Awaited -> Qualified) to notification bhejo
        if (!string.Equals(
                previousResult,
                updated.Result,
                StringComparison.OrdinalIgnoreCase))
        {
            await TryNotifyAsync(updated);
        }

        return updated;
    }

    // Notification fail ho to result save par koi asar nahi padta
    private async Task TryNotifyAsync(ExamResult result)
    {
        try
        {
            await _notifications.NotifyResultAsync(result);
        }
        catch
        {
            // ignore
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}
