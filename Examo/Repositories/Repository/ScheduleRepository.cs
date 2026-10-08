using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ScheduleRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<Schedule>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Schedules
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<Schedule?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Schedules
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<Schedule> AddAsync(Schedule schedule)
    {
        // Record hamesha current user ke naam par banta hai
        schedule.UserId = _currentUser.UserId;

        await _context.Schedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        return schedule;
    }

    public async Task<Schedule> UpdateAsync(Schedule schedule)
    {
        if (schedule.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Schedules.Update(schedule);
        await _context.SaveChangesAsync();

        return schedule;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var schedule = await _context.Schedules
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (schedule == null)
        {
            return false;
        }

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return true;
    }
}
