using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ExamFormRepository : IExamFormRepository
{
    private readonly ExamoDbContext _context;

    public ExamFormRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ExamForm>> GetAllAsync()
    {
        return await _context.ExamForms
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ExamForm?> GetByIdAsync(int id)
    {
        return await _context.ExamForms
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<ExamForm> AddAsync(ExamForm examForm)
    {
        await _context.ExamForms.AddAsync(examForm);
        await _context.SaveChangesAsync();

        return examForm;
    }

    public async Task<ExamForm> UpdateAsync(ExamForm examForm)
    {
        _context.ExamForms.Update(examForm);
        await _context.SaveChangesAsync();

        return examForm;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var examForm = await _context.ExamForms
            .FirstOrDefaultAsync(x => x.Id == id);

        if (examForm == null)
        {
            return false;
        }

        _context.ExamForms.Remove(examForm);
        await _context.SaveChangesAsync();

        return true;
    }
}