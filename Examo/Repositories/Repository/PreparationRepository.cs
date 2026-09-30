using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class PreparationRepository : IPreparationRepository
{
    private readonly ExamoDbContext _context;

    public PreparationRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Preparation>> GetAllAsync()
    {
        return await _context.Preparations
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Preparation?> GetByIdAsync(int id)
    {
        return await _context.Preparations
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Preparation> AddAsync(Preparation preparation)
    {
        await _context.Preparations.AddAsync(preparation);
        await _context.SaveChangesAsync();

        return preparation;
    }

    public async Task<Preparation> UpdateAsync(Preparation preparation)
    {
        _context.Preparations.Update(preparation);
        await _context.SaveChangesAsync();

        return preparation;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var preparation = await _context.Preparations
            .FirstOrDefaultAsync(x => x.Id == id);

        if (preparation == null)
        {
            return false;
        }

        _context.Preparations.Remove(preparation);
        await _context.SaveChangesAsync();

        return true;
    }
}