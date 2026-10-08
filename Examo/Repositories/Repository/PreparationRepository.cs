using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class PreparationRepository : IPreparationRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PreparationRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<Preparation>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Preparations
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<Preparation?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Preparations
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<Preparation> AddAsync(Preparation preparation)
    {
        // Record hamesha current user ke naam par banta hai
        preparation.UserId = _currentUser.UserId;

        await _context.Preparations.AddAsync(preparation);
        await _context.SaveChangesAsync();

        return preparation;
    }

    public async Task<Preparation> UpdateAsync(Preparation preparation)
    {
        if (preparation.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Preparations.Update(preparation);
        await _context.SaveChangesAsync();

        return preparation;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var preparation = await _context.Preparations
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (preparation == null)
        {
            return false;
        }

        _context.Preparations.Remove(preparation);
        await _context.SaveChangesAsync();

        return true;
    }
}
