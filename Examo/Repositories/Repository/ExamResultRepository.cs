using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ExamResultRepository : IExamResultRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExamResultRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<ExamResult>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Results
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<ExamResult?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Results
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<ExamResult> AddAsync(ExamResult result)
    {
        // Record hamesha current user ke naam par banta hai
        result.UserId = _currentUser.UserId;

        await _context.Results.AddAsync(result);
        await _context.SaveChangesAsync();

        return result;
    }

    public async Task<ExamResult> UpdateAsync(ExamResult result)
    {
        if (result.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Results.Update(result);
        await _context.SaveChangesAsync();

        return result;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var result = await _context.Results
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (result == null)
        {
            return false;
        }

        _context.Results.Remove(result);
        await _context.SaveChangesAsync();

        return true;
    }
}
