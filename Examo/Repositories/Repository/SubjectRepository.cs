using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class SubjectRepository : ISubjectRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubjectRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<Subject>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Subjects
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<Subject?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Subjects
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<Subject> AddAsync(Subject subject)
    {
        // Record hamesha current user ke naam par banta hai
        subject.UserId = _currentUser.UserId;

        await _context.Subjects.AddAsync(subject);
        await _context.SaveChangesAsync();

        return subject;
    }

    public async Task<Subject> UpdateAsync(Subject subject)
    {
        if (subject.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Subjects.Update(subject);
        await _context.SaveChangesAsync();

        return subject;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (subject == null)
        {
            return false;
        }

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();

        return true;
    }
}
