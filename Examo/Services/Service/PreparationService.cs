
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

    public PreparationService(
        IPreparationRepository repository,
        ExamoDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<IEnumerable<Preparation>> GetAllAsync()
    {
        // ExamForm ke saare ExamName Preparation me sync karo
        var examFormNames = await _context.ExamForms
            .AsNoTracking()
            .Select(x => x.ExamName)
            .Distinct()
            .ToListAsync();

        var existingPreparationNames = await _context.Preparations
            .Select(x => x.ExamName)
            .ToListAsync();

        foreach (var examName in examFormNames)
        {
            if (!existingPreparationNames.Contains(examName))
            {
                var preparation = new Preparation
                {
                    ExamName = examName,
                    Status = "Not Started"
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
        preparation.Status = dto.Status;

        return await _repository.UpdateAsync(preparation);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}

