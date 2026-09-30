using Examo.Models;
using Examo.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly ExamoDbContext _context;

    public ScheduleRepository(ExamoDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Schedule>> GetAllAsync()
    {
        return await _context.Schedules
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Schedule?> GetByIdAsync(int id)
    {
        return await _context.Schedules
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Schedule> AddAsync(Schedule schedule)
    {
        await _context.Schedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        return schedule;
    }

    public async Task<Schedule> UpdateAsync(Schedule schedule)
    {
        _context.Schedules.Update(schedule);
        await _context.SaveChangesAsync();

        return schedule;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var schedule = await _context.Schedules
            .FirstOrDefaultAsync(x => x.Id == id);

        if (schedule == null)
        {
            return false;
        }

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return true;
    }
}