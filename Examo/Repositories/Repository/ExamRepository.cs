using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ExamRepository : IExamRepository
{
    private readonly ExamoDbContext _context;

    public ExamRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Exam>> GetAllAsync()
    {
        return await _context.Exams
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Exam?> GetByIdAsync(int id)
    {
        return await _context.Exams
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Exam> AddAsync(Exam exam)
    {
        await _context.Exams.AddAsync(exam);
        await _context.SaveChangesAsync();

        return exam;
    }

    public async Task<Exam> UpdateAsync(Exam exam)
    {
        _context.Exams.Update(exam);
        await _context.SaveChangesAsync();

        return exam;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(x => x.Id == id);

        if (exam == null)
        {
            return false;
        }

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();

        return true;
    }
}