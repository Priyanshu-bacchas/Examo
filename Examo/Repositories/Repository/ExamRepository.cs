using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ExamRepository : IExamRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExamRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<Exam>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Exams
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<Exam?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Exams
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<Exam> AddAsync(Exam exam)
    {
        // Record hamesha current user ke naam par banta hai
        exam.UserId = _currentUser.UserId;

        await _context.Exams.AddAsync(exam);
        await _context.SaveChangesAsync();

        return exam;
    }

    public async Task<Exam> UpdateAsync(Exam exam)
    {
        if (exam.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Exams.Update(exam);
        await _context.SaveChangesAsync();

        return exam;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var exam = await _context.Exams
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (exam == null)
        {
            return false;
        }

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();

        return true;
    }
}
