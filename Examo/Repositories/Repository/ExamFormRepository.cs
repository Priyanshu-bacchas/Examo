using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ExamFormRepository : IExamFormRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExamFormRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<ExamForm>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.ExamForms
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<ExamForm?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.ExamForms
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<ExamForm> AddAsync(ExamForm examForm)
    {
        // Record hamesha current user ke naam par banta hai
        examForm.UserId = _currentUser.UserId;

        await _context.ExamForms.AddAsync(examForm);
        await _context.SaveChangesAsync();

        return examForm;
    }

    public async Task<ExamForm> UpdateAsync(ExamForm examForm)
    {
        if (examForm.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.ExamForms.Update(examForm);
        await _context.SaveChangesAsync();

        return examForm;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var examForm = await _context.ExamForms
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (examForm == null)
        {
            return false;
        }

        _context.ExamForms.Remove(examForm);
        await _context.SaveChangesAsync();

        return true;
    }
}
