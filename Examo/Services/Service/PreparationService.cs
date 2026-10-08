using Examo.DTOs.Preparation;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

public class PreparationService : IPreparationService
{
    private readonly IPreparationRepository _repository;
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PreparationService(
        IPreparationRepository repository,
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<Preparation>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        // Sirf is user ke ExamForm names Preparation me sync karo
        var examFormNames = await _context.ExamForms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ExamName)
            .Distinct()
            .ToListAsync();

        var existingPreparationNames = await _context.Preparations
            .Where(x => x.UserId == userId)
            .Select(x => x.ExamName)
            .ToListAsync();

        foreach (var examName in examFormNames)
        {
            if (!existingPreparationNames.Contains(examName))
            {
                var preparation = new Preparation
                {
                    ExamName = examName,
                    Syllabus = null,
                    Status = "Not Started",
                    UserId = userId
                };

                await _context.Preparations.AddAsync(preparation);
            }
        }

        await _context.SaveChangesAsync();

        return await _repository.GetAllAsync();
    }

    public async Task<Preparation?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Preparation> AddAsync(
        PreparationCreateDto dto)
    {
        var preparation = new Preparation
        {
            ExamName = dto.ExamName,
            Syllabus = dto.Syllabus,
            Status = dto.Status
        };

        return await _repository.AddAsync(preparation);
    }

    public async Task<Preparation?> UpdateAsync(
        int id,
        PreparationUpdateDto dto)
    {
        var preparation = await _repository.GetByIdAsync(id);

        if (preparation == null)
        {
            return null;
        }

        preparation.ExamName = dto.ExamName;
        preparation.Syllabus = dto.Syllabus;
        preparation.Status = dto.Status;

        return await _repository.UpdateAsync(preparation);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}