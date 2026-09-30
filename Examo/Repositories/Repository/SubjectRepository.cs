using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class SubjectRepository : ISubjectRepository
{
    private readonly ExamoDbContext _context;

    public SubjectRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Subject>> GetAllAsync()
    {
        return await _context.Subjects
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Subject?> GetByIdAsync(int id)
    {
        return await _context.Subjects
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Subject> AddAsync(Subject subject)
    {
        await _context.Subjects.AddAsync(subject);
        await _context.SaveChangesAsync();

        return subject;
    }

    public async Task<Subject> UpdateAsync(Subject subject)
    {
        _context.Subjects.Update(subject);
        await _context.SaveChangesAsync();

        return subject;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var subject = await _context.Subjects
            .FirstOrDefaultAsync(x => x.Id == id);

        if (subject == null)
        {
            return false;
        }

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();

        return true;
    }
}